using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;
using UnityEngine;

using Flynn.Core;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc.Memory
{
    // Thin wrapper over a single LiteDB file holding one save slot's NPC memory.
    // Owns the connection lifecycle (Open/Dispose), exposes typed helpers for the
    // collections used this pass, and a generic GetCollection<T> so future doc
    // types (relationships, player state) slot in without touching this class.
    //
    // Vectors are stored on the documents; recall is a brute-force cosine over a
    // Find-filtered candidate set — fast at NPC scale, no native vector index.
    public class NpcMemoryDatabase : IDisposable
    {
        public const int SchemaVersion = 1;

        private LiteDatabase _db;
        private string _path;

        public bool IsOpen => _db != null;
        public string Path => _path;

        // One recalled item ready to render into the prompt's memory block.
        public struct RecalledItem
        {
            public string Text;
            public string Subject;  // memory subject tag, or "knowledge"
            public string Source;   // dialogue | authored | gossip | knowledge
            public float Score;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public static string SlotDir(string slotId)
        {
            string slot = string.IsNullOrWhiteSpace(slotId) ? "slot_0" : slotId.Trim();
            return System.IO.Path.Combine(Application.persistentDataPath, "npc_memory", slot);
        }

        public static string SlotDbPath(string slotId) =>
            System.IO.Path.Combine(SlotDir(slotId), "npc_memory.db");

        // Slot ids that have a DB file on disk (dir names under npc_memory/).
        public static IEnumerable<string> EnumerateSlots()
        {
            string root = System.IO.Path.Combine(Application.persistentDataPath, "npc_memory");
            if (!Directory.Exists(root)) yield break;
            foreach (var dir in Directory.GetDirectories(root))
                if (File.Exists(System.IO.Path.Combine(dir, "npc_memory.db")))
                    yield return System.IO.Path.GetFileName(dir);
        }

        // Opens (creating if needed) the DB file for a slot under persistentDataPath.
        public void Open(string slotId, string embeddingModel, int dim)
        {
            OpenInternal(slotId);
            EnsureIndices();
            EnsureMeta(embeddingModel, dim);
        }

        // Opens an existing slot DB for read/manage (browser/editor) without
        // writing meta. Returns false if the file doesn't exist.
        public bool OpenExisting(string slotId)
        {
            if (!File.Exists(SlotDbPath(slotId))) return false;
            OpenInternal(slotId);
            return true;
        }

        private void OpenInternal(string slotId)
        {
            Dispose();
            Directory.CreateDirectory(SlotDir(slotId));
            _path = SlotDbPath(slotId);
            // Shared connection so an external LiteDB browser (or a second
            // in-process connection) can open the file while the game holds it.
            _db = new LiteDatabase(new ConnectionString { Filename = _path, Connection = ConnectionType.Shared });
        }

        private void EnsureIndices()
        {
            Memories.EnsureIndex(x => x.NpcId);
            Knowledge.EnsureIndex(x => x.OwnerScope);
            Knowledge.EnsureIndex(x => x.ContentHash);
            ChatTurns.EnsureIndex(x => x.NpcId);
            Things.EnsureIndex(x => x.ThingId);
            Npcs.EnsureIndex(x => x.NpcId);
            Signals.EnsureIndex(x => x.SignalId);
            Community.EnsureIndex(x => x.CommunityId);
            FiredSignals.EnsureIndex(x => x.NpcId);
            PlayerCodex.EnsureIndex(x => x.SourceNpcId);
            PlayerCodex.EnsureIndex(x => x.ContentHash);
        }

        private void EnsureMeta(string embeddingModel, int dim)
        {
            var col = Meta;
            var meta = col.FindById(1);
            if (meta == null)
            {
                col.Insert(new MetaDoc { Id = 1, SchemaVersion = SchemaVersion, EmbeddingModel = embeddingModel, Dim = dim });
                return;
            }
            if (!string.Equals(meta.EmbeddingModel, embeddingModel, StringComparison.Ordinal) || meta.Dim != dim)
                Debug.LogWarning(string.Format(
                    "[NpcMemoryDatabase] DB was built with embedding model '{0}' ({1}-dim) but current settings are '{2}' ({3}-dim). " +
                    "Stored vectors won't be comparable to new ones — re-embed or clear the DB.",
                    meta.EmbeddingModel, meta.Dim, embeddingModel, dim));
        }

        public void Dispose()
        {
            if (_db != null) { _db.Dispose(); _db = null; }
        }

        // ── Collection accessors ──────────────────────────────────────────────

        public ILiteCollection<MemoryDoc> Memories  => _db.GetCollection<MemoryDoc>(MemoryCollections.Memories);
        public ILiteCollection<KnowledgeDoc> Knowledge => _db.GetCollection<KnowledgeDoc>(MemoryCollections.Knowledge);
        public ILiteCollection<ChatTurnDoc> ChatTurns => _db.GetCollection<ChatTurnDoc>(MemoryCollections.ChatTurns);
        public ILiteCollection<MetaDoc> Meta        => _db.GetCollection<MetaDoc>(MemoryCollections.Meta);

        // Authored entities.
        public ILiteCollection<CommunityDoc> Community => _db.GetCollection<CommunityDoc>(MemoryCollections.Community);
        public ILiteCollection<ThingDoc> Things        => _db.GetCollection<ThingDoc>(MemoryCollections.Things);
        public ILiteCollection<NpcDoc> Npcs            => _db.GetCollection<NpcDoc>(MemoryCollections.Npcs);
        public ILiteCollection<SignalDoc> Signals      => _db.GetCollection<SignalDoc>(MemoryCollections.Signals);
        public ILiteCollection<FiredSignalDoc> FiredSignals => _db.GetCollection<FiredSignalDoc>(MemoryCollections.FiredSignals);

        // Player-facing codex entries.
        public ILiteCollection<PlayerCodexEntry> PlayerCodex => _db.GetCollection<PlayerCodexEntry>(MemoryCollections.PlayerCodex);

        // Generic escape hatch for doc types added later (RelationshipDoc, PlayerStateDoc).
        public ILiteCollection<T> GetCollection<T>(string name) => _db.GetCollection<T>(name);

        // Distinct NPC ids that have any dynamic data (memories or chat).
        public IEnumerable<string> DistinctNpcIds()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in Memories.FindAll()) if (!string.IsNullOrEmpty(m.NpcId)) set.Add(m.NpcId);
            foreach (var t in ChatTurns.FindAll()) if (!string.IsNullOrEmpty(t.NpcId)) set.Add(t.NpcId);
            return set;
        }

        // ── Memory writes ─────────────────────────────────────────────────────

        // Inserts a fact unless a near-duplicate (cosine >= dedupThreshold) already
        // exists for the same NPC + subject. Returns true if inserted.
        public bool TryInsertMemory(MemoryDoc doc, float dedupThreshold)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.Text)) return false;

            if (doc.Embedding != null && doc.Embedding.Length > 0)
            {
                foreach (var existing in Memories.Find(x => x.NpcId == doc.NpcId && x.Subject == doc.Subject))
                {
                    if (existing.Embedding == null) continue;
                    if (VectorMath.Cosine(existing.Embedding, doc.Embedding) >= dedupThreshold)
                        return false;
                }
            }

            if (doc.CreatedUtc == default) doc.CreatedUtc = DateTime.UtcNow;
            Memories.Insert(doc);
            return true;
        }

        public void InsertChatTurn(ChatTurnDoc doc)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.Content)) return;
            if (doc.CreatedUtc == default) doc.CreatedUtc = DateTime.UtcNow;
            ChatTurns.Insert(doc);
        }

        public int NextTurnIndex(string npcId)
        {
            int max = -1;
            foreach (var t in ChatTurns.Find(x => x.NpcId == npcId))
                if (t.TurnIndex > max) max = t.TurnIndex;
            return max + 1;
        }

        // ── Recall ────────────────────────────────────────────────────────────

        // Top-k blended-score recall over this NPC's memories + visible knowledge.
        // Bumps recall stats on the memories that survive into the result.
        public List<RecalledItem> Recall(string npcId, int trust, float[] queryVec, int topK, float minSimilarity)
        {
            var results = new List<RecalledItem>();
            if (_db == null || queryVec == null || queryVec.Length == 0 || string.IsNullOrEmpty(npcId))
                return results;

            DateTime now = DateTime.UtcNow;
            var scoredMemories = new List<KeyValuePair<MemoryDoc, float>>();

            foreach (var m in Memories.Find(x => x.NpcId == npcId))
            {
                if (m.Embedding == null) continue;
                float sim = VectorMath.Cosine(queryVec, m.Embedding);
                if (sim < minSimilarity) continue;

                float importanceBoost = 0.05f * Mathf.Clamp(m.Importance, 0f, 5f);
                double ageDays = (now - m.CreatedUtc).TotalDays;
                float recencyBoost = Mathf.Clamp01(1f - (float)(ageDays / 30.0)) * 0.1f;
                float score = sim + importanceBoost + recencyBoost;

                scoredMemories.Add(new KeyValuePair<MemoryDoc, float>(m, score));
                results.Add(new RecalledItem { Text = m.Text, Subject = m.Subject, Source = m.Source, Score = score });
            }

            foreach (var k in Knowledge.Find(x => x.OwnerScope == npcId || x.OwnerScope == "community"))
            {
                if (k.Embedding == null) continue;
                if (k.RevealTrust > 0 && trust < k.RevealTrust) continue;
                float sim = VectorMath.Cosine(queryVec, k.Embedding);
                if (sim < minSimilarity) continue;
                results.Add(new RecalledItem { Text = k.Text, Subject = "knowledge", Source = "knowledge", Score = sim });
            }

            results.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (topK > 0 && results.Count > topK) results.RemoveRange(topK, results.Count - topK);

            // Bump recall stats for memories that made the cut.
            var keep = new HashSet<string>();
            foreach (var r in results) keep.Add(r.Text);
            foreach (var kv in scoredMemories)
            {
                if (!keep.Contains(kv.Key.Text)) continue;
                kv.Key.LastRecalledUtc = now;
                kv.Key.RecallCount += 1;
                Memories.Update(kv.Key);
            }

            return results;
        }

        // ── Stats (for HUD / editor read-outs) ────────────────────────────────

        public int CountMemories(string npcId)
        {
            if (_db == null || string.IsNullOrEmpty(npcId)) return 0;
            return Memories.Count(x => x.NpcId == npcId);
        }

        public int CountChatTurns(string npcId)
        {
            if (_db == null || string.IsNullOrEmpty(npcId)) return 0;
            return ChatTurns.Count(x => x.NpcId == npcId);
        }

        // Most recent chat turn's text for this NPC, or "" if none.
        public string GetLastChatTurnText(string npcId)
        {
            if (_db == null || string.IsNullOrEmpty(npcId)) return "";
            ChatTurnDoc latest = null;
            foreach (var t in ChatTurns.Find(x => x.NpcId == npcId))
                if (latest == null || t.TurnIndex > latest.TurnIndex) latest = t;
            return latest != null ? latest.Content : "";
        }

        // ── Maintenance ───────────────────────────────────────────────────────

        public void ClearNpc(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            Memories.DeleteMany(x => x.NpcId == npcId);
            ChatTurns.DeleteMany(x => x.NpcId == npcId);
            FiredSignals.DeleteMany(x => x.NpcId == npcId);
        }

        public void ClearAllMemory()
        {
            Memories.DeleteAll();
            ChatTurns.DeleteAll();
            FiredSignals.DeleteAll();
        }

        // ── Fired-signal tracking ─────────────────────────────────────────────

        public bool WasTriggerFired(string npcId, string signalId)
        {
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(signalId)) return false;
            return FiredSignals.Exists(x => x.NpcId == npcId && x.SignalId == signalId);
        }

        public List<FiredSignalDoc> GetAllFiredSignals()
        {
            return new List<FiredSignalDoc>(FiredSignals.FindAll());
        }

        public void MarkTriggerFired(string npcId, string signalId)
        {
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(signalId)) return;
            if (WasTriggerFired(npcId, signalId)) return;
            FiredSignals.Insert(new FiredSignalDoc
            {
                NpcId = npcId,
                SignalId = signalId,
                FiredUtc = DateTime.UtcNow,
            });
        }

        // All knowledge rows visible to this NPC (owner or community), WITHOUT trust
        // filtering — callers read RevealTrust themselves to render locked-topic
        // teases ("secret — trust 60") without leaking the text.
        public List<KnowledgeDoc> GetKnowledgeMeta(string npcId)
        {
            var list = new List<KnowledgeDoc>();
            if (_db == null || string.IsNullOrEmpty(npcId)) return list;
            foreach (var k in Knowledge.Find(x => x.OwnerScope == npcId || x.OwnerScope == "community"))
                list.Add(k);
            return list;
        }

        // ── Player codex ──────────────────────────────────────────────────────

        // Inserts a codex entry unless a duplicate (same ContentHash) exists.
        // Returns the entry Id, or 0 if skipped.
        public int InsertCodexEntry(PlayerCodexEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.FactText)) return 0;

            if (entry.LearnedUtc == default) entry.LearnedUtc = DateTime.UtcNow;
            if (string.IsNullOrEmpty(entry.ContentHash))
                entry.ContentHash = StableHash(entry.FactText);

            // Dedup by content hash — same fact from the same NPC shouldn't duplicate.
            if (PlayerCodex.Exists(x => x.ContentHash == entry.ContentHash && x.SourceNpcId == entry.SourceNpcId))
                return 0;

            return PlayerCodex.Insert(entry);
        }

        public List<PlayerCodexEntry> GetAllCodexEntries()
        {
            var list = new List<PlayerCodexEntry>();
            if (_db == null) return list;
            foreach (var e in PlayerCodex.FindAll()) list.Add(e);
            return list;
        }

        public List<PlayerCodexEntry> GetCodexEntriesByNpc(string npcId)
        {
            var list = new List<PlayerCodexEntry>();
            if (_db == null || string.IsNullOrEmpty(npcId)) return list;
            foreach (var e in PlayerCodex.Find(x => x.SourceNpcId == npcId)) list.Add(e);
            return list;
        }

        public bool IsCodexEntryKnown(string factText)
        {
            if (string.IsNullOrWhiteSpace(factText)) return false;
            string hash = StableHash(factText);
            return PlayerCodex.Exists(x => x.ContentHash == hash);
        }

        // Flips an encrypted entry to revealed by setting IsEncrypted=false.
        public void MarkCodexEntryTranslated(int entryId, string translatedText)
        {
            var entry = PlayerCodex.FindById(entryId);
            if (entry == null) return;
            entry.IsEncrypted = false;
            if (!string.IsNullOrWhiteSpace(translatedText))
                entry.FactText = translatedText;
            PlayerCodex.Update(entry);
        }

        public int CountCodexEntries(string npcId)
        {
            if (_db == null || string.IsNullOrEmpty(npcId)) return 0;
            return PlayerCodex.Count(x => x.SourceNpcId == npcId);
        }

        public int CountUnviewedCodexEntries()
        {
            if (_db == null) return 0;
            return PlayerCodex.Count(x => x.HasBeenViewed == false);
        }

        public void MarkAllCodexEntriesViewed()
        {
            if (_db == null || !IsOpen) return;
            try
            {
                foreach (var entry in PlayerCodex.FindAll())
                {
                    if (!entry.HasBeenViewed)
                    {
                        entry.HasBeenViewed = true;
                        PlayerCodex.Update(entry);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[NpcMemoryDatabase] MarkAllCodexEntriesViewed failed (DB disposed?): " + e.Message);
            }
        }

        private static string StableHash(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            // Simple deterministic hash — not crypto, just for dedup.
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text.Trim()));
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
