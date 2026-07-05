using System;
using System.Collections.Generic;
using UnityEngine;
using Flynn.Npc.Memory;

namespace Flynn.Npc
{
    // Player-facing knowledge journal. Captures facts the player learns from NPC
    // dialogue (disclosure/world subjects) and from scanning world objects.
    // Tracks per-NPC secret counts for the "Secrets 2/8" progression display.
    //
    // Lives as a singleton on MANAGERS. The DialogueManager calls
    // OnDialogueTurnCompleted after each envelope is parsed; the codex filters
    // for disclosure/world facts and persists them.
    public class PlayerCodex : MonoBehaviour
    {
        public static PlayerCodex Instance { get; private set; }

        // Fired when a new entry is added — UI controllers subscribe to refresh.
        public event Action<PlayerCodexEntry> OnEntryAdded;
        // Fired when an encrypted entry is translated.
        public event Action<PlayerCodexEntry> OnEntryTranslated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerCodex] Duplicate; destroying " + name);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Called by DialogueManager after an envelope is parsed. Filters for
        // disclosure (NPC told you something) and world (world fact) subjects.
        public void OnDialogueTurnCompleted(string npcId, string topic, NpcReplyEnvelope envelope)
        {
            if (envelope == null || envelope.memory_updates == null)
            {
                Debug.Log($"[PlayerCodex] OnDialogueTurnCompleted: npcId={npcId}, envelope null or no memory_updates");
                return;
            }
            if (string.IsNullOrEmpty(npcId)) return;

            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.MemoryDb == null || !sceneLlm.MemoryDb.IsOpen)
            {
                Debug.LogWarning($"[PlayerCodex] OnDialogueTurnCompleted: SceneLlmManager or MemoryDb not available");
                return;
            }

            Debug.Log($"[PlayerCodex] OnDialogueTurnCompleted: npcId={npcId}, topic={topic ?? "(null)"}, {envelope.memory_updates.Length} memory_updates");

            foreach (var update in envelope.memory_updates)
            {
                if (update == null || string.IsNullOrWhiteSpace(update.fact)) continue;

                string subject = NormalizeSubject(update.subject);
                Debug.Log($"[PlayerCodex]   memory_update: subject='{subject}', fact='{update.fact.Substring(0, System.Math.Min(60, update.fact.Length))}'");

                // Capture ALL memory updates — from the player's perspective, anything
                // the NPC says is knowledge they learned. The subject field is used for
                // categorization in the codex UI, not for filtering.
                var entry = new PlayerCodexEntry
                {
                    SourceNpcId = npcId,
                    Topic = topic ?? "",
                    FactText = update.fact.Trim(),
                    Kind = subject,
                    IsEncrypted = false,
                    LearnedUtc = DateTime.UtcNow,
                };

                int id = sceneLlm.MemoryDb.InsertCodexEntry(entry);
                if (id > 0)
                {
                    Debug.Log($"[PlayerCodex]     ADDED entry id={id}: {entry.FactText}");
                    OnEntryAdded?.Invoke(entry);
                }
                else
                {
                    Debug.Log($"[PlayerCodex]     DEDUPED (already exists)");
                }
            }
        }

        // Normalizes the subject field — handles the LLM sometimes returning the
        // literal "player|self|world|relationship|disclosure" template string.
        private static string NormalizeSubject(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject)) return "world";
            string s = subject.Trim().ToLowerInvariant();
            // Handle the LLM copying the template literal
            if (s.Contains("|")) return "world";
            if (s == "player" || s == "self" || s == "world" || s == "relationship" || s == "disclosure") return s;
            if (s == "npc" || s == "me") return "self";
            if (s == "user") return "player";
            if (s == "told" || s == "shared" || s == "revealed") return "disclosure";
            return "world";
        }

        // Called by ScanTarget (or a scan controller) when an encrypted fragment
        // is scanned. Stores the jumbled text until an NPC translates it.
        public void AddScanFragment(string thingId, string displayName, string jumbledText)
        {
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.MemoryDb == null || !sceneLlm.MemoryDb.IsOpen) return;

            var entry = new PlayerCodexEntry
            {
                SourceNpcId = "scan",
                Topic = displayName ?? thingId ?? "",
                ThingId = thingId ?? "",
                FactText = "", // Real text filled in on translation
                EncryptedText = jumbledText,
                Kind = "fact",
                IsEncrypted = true,
                LearnedUtc = DateTime.UtcNow,
            };

            int id = sceneLlm.MemoryDb.InsertCodexEntry(entry);
            if (id > 0)
            {
                Debug.Log("[PlayerCodex] Scan fragment added: " + jumbledText);
                OnEntryAdded?.Invoke(entry);
            }
        }

        // Called when an NPC translates an encrypted fragment. Finds the entry
        // by thingId and reveals the real text.
        public void TranslateScanFragment(string thingId, string translatedText)
        {
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.MemoryDb == null || !sceneLlm.MemoryDb.IsOpen) return;

            foreach (var entry in sceneLlm.MemoryDb.GetCollection<PlayerCodexEntry>(MemoryCollections.PlayerCodex).FindAll())
            {
                if (!entry.IsEncrypted) continue;
                if (!string.Equals(entry.ThingId, thingId, StringComparison.Ordinal)) continue;

                sceneLlm.MemoryDb.MarkCodexEntryTranslated(entry.Id, translatedText);
                entry.IsEncrypted = false;
                entry.FactText = translatedText;
                Debug.Log("[PlayerCodex] Fragment translated: " + translatedText);
                OnEntryTranslated?.Invoke(entry);
                return;
            }
        }

        // ── Query API for UI ──────────────────────────────────────────────────

        public List<PlayerCodexEntry> GetAllEntries()
        {
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.MemoryDb == null) return new List<PlayerCodexEntry>();
            return sceneLlm.MemoryDb.GetAllCodexEntries();
        }

        public List<PlayerCodexEntry> GetEntriesForNpc(string npcId)
        {
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.MemoryDb == null) return new List<PlayerCodexEntry>();
            return sceneLlm.MemoryDb.GetCodexEntriesByNpc(npcId);
        }

        // Total authored secrets for an NPC (from island JSON knowledge entries
        // where kind == "secret"). Used for the "Secrets X/Y" counter.
        public int GetTotalSecretCount(string npcId)
        {
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.islandContent == null) return 0;

            var npc = sceneLlm.islandContent.GetNpc(npcId);
            if (npc == null || npc.knowledge == null) return 0;

            int count = 0;
            foreach (var k in npc.knowledge)
                if (k != null && string.Equals(k.kind, "secret", StringComparison.OrdinalIgnoreCase))
                    count++;
            return count;
        }

        // How many secrets the player has unlocked (codex entries from this NPC
        // with kind == "disclosure" or kind == "secret").
        public int GetRevealedSecretCount(string npcId)
        {
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.MemoryDb == null) return 0;

            int count = 0;
            foreach (var entry in sceneLlm.MemoryDb.GetCodexEntriesByNpc(npcId))
                if (string.Equals(entry.Kind, "disclosure", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.Kind, "secret", StringComparison.OrdinalIgnoreCase))
                    count++;
            return count;
        }

        // All authored knowledge entries for an NPC that are still locked (trust
        // too low). Returns list of (kind, text, revealTrustThreshold) for UI display.
        public List<LockedKnowledge> GetLockedSecrets(string npcId, int currentTrust)
        {
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.islandContent == null) return new List<LockedKnowledge>();

            var npc = sceneLlm.islandContent.GetNpc(npcId);
            if (npc == null || npc.knowledge == null) return new List<LockedKnowledge>();

            var result = new List<LockedKnowledge>();
            foreach (var k in npc.knowledge)
            {
                if (k == null) continue;
                if (k.revealTrustThreshold <= 0) continue;
                if (currentTrust >= k.revealTrustThreshold) continue;
                // Only show locked entries that have a trust threshold — these are the "???" entries
                result.Add(new LockedKnowledge
                {
                    Kind = k.kind,
                    Threshold = k.revealTrustThreshold,
                    ThingId = k.thingId ?? "",
                    DisplayName = ResolveThingDisplayName(k.thingId),
                });
            }
            return result;
        }

        public struct LockedKnowledge
        {
            public string Kind;
            public int Threshold;
            public string ThingId;
            public string DisplayName;
        }

        private string ResolveThingDisplayName(string thingId)
        {
            if (string.IsNullOrEmpty(thingId)) return "something";
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.islandContent == null) return thingId;
            var thing = sceneLlm.islandContent.GetThing(thingId);
            return thing != null ? thing.displayName : thingId;
        }

        public int GetUnviewedCount()
        {
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.MemoryDb == null) return 0;
            return sceneLlm.MemoryDb.CountUnviewedCodexEntries();
        }

        public void MarkAllEntriesViewed()
        {
            var sceneLlm = SceneLlmManager.Instance;
            if (sceneLlm == null || sceneLlm.MemoryDb == null) return;
            sceneLlm.MemoryDb.MarkAllCodexEntriesViewed();
        }
    }
}
