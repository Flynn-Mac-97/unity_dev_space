using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Flynn.Npc.Memory;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Npc
{
    // Mirrors authored island content (community, things, NPCs, signals, knowledge)
    // from the parsed IslandContent POCOs into the unified LiteDB. Idempotent: each
    // row carries a ContentHash, so unchanged entries are skipped and only new or
    // edited entries cost an embedding call. Dynamic rows (memories/chat) are never
    // touched here.
    //
    // Coroutine-based because embedding is an async HTTP round-trip (Ollama). Drive
    // it from a MonoBehaviour: StartCoroutine(IslandDbImporter.Import(content, db, embedder)).
    public static class IslandDbImporter
    {
        public static IEnumerator Import(IslandContent content, NpcMemoryDatabase db, OllamaEmbeddingProvider embedder)
        {
            if (content == null || db == null || embedder == null) yield break;
            string islandId = content.islandId ?? string.Empty;
            int embedded = 0, skipped = 0;

            // ── Community ──────────────────────────────────────────────────────────
            var community = content.community;
            if (community != null)
            {
                string hash = SceneLlmManager.ContentHash(islandId, "community", community.communityId,
                    community.displayName, community.overview, community.currentSituation, community.sharedMood, community.culture);
                var existing = db.Community.FindOne(x => x.CommunityId == community.communityId && x.IslandId == islandId);
                if (existing == null || existing.ContentHash != hash)
                {
                    float[] vec = null;
                    yield return EmbedInto(embedder, Join(community.overview, community.currentSituation), v => vec = v);
                    var doc = new CommunityDoc
                    {
                        Id = existing != null ? existing.Id : 0,
                        IslandId = islandId,
                        CommunityId = community.communityId,
                        DisplayName = community.displayName,
                        Overview = community.overview,
                        CurrentSituation = community.currentSituation,
                        SharedMood = community.sharedMood,
                        Culture = community.culture,
                        ContentHash = hash,
                        Embedding = vec,
                    };
                    db.Community.Upsert(doc);
                    embedded++;
                }
                else skipped++;
            }

            // ── Things ─────────────────────────────────────────────────────────────
            if (content.things != null)
                foreach (var t in content.things)
                {
                    if (t == null || string.IsNullOrEmpty(t.thingId)) continue;
                    string hash = SceneLlmManager.ContentHash(islandId, "thing", t.thingId, t.displayName,
                        t.shortDescription, Join(t.aliases), Join(t.tags));
                    var existing = db.Things.FindOne(x => x.ThingId == t.thingId && x.IslandId == islandId);
                    if (existing != null && existing.ContentHash == hash) { skipped++; continue; }

                    float[] vec = null;
                    yield return EmbedInto(embedder, Join(t.displayName, Join(t.aliases), t.shortDescription), v => vec = v);
                    db.Things.Upsert(new ThingDoc
                    {
                        Id = existing != null ? existing.Id : 0,
                        IslandId = islandId,
                        ThingId = t.thingId,
                        DisplayName = t.displayName,
                        Aliases = t.aliases,
                        Tags = t.tags,
                        ShortDescription = t.shortDescription,
                        ContentHash = hash,
                        Embedding = vec,
                    });
                    embedded++;
                }

            // ── NPCs ─────────────────────────────────────────────────────────────
            if (content.npcs != null)
                foreach (var n in content.npcs)
                {
                    if (n == null || string.IsNullOrEmpty(n.npcId)) continue;
                    string notesJson = n.designerNotes != null ? JsonUtility.ToJson(n.designerNotes) : null;
                    string hash = SceneLlmManager.ContentHash(islandId, "npc", n.npcId, n.displayName, n.role,
                        n.speakingStyle, Join(n.personalityTraits), Join(n.doRules), Join(n.dontRules), Join(n.capabilities),
                        n.startingTrust.ToString(), n.trustToShareSecrets.ToString(), n.fallbackReply, notesJson);
                    var existing = db.Npcs.FindOne(x => x.NpcId == n.npcId && x.IslandId == islandId);
                    if (existing != null && existing.ContentHash == hash) { skipped++; continue; }

                    float[] vec = null;
                    yield return EmbedInto(embedder, Join(n.displayName, n.role, n.speakingStyle, Join(n.personalityTraits)), v => vec = v);
                    db.Npcs.Upsert(new NpcDoc
                    {
                        Id = existing != null ? existing.Id : 0,
                        IslandId = islandId,
                        NpcId = n.npcId,
                        DisplayName = n.displayName,
                        Role = n.role,
                        SpeakingStyle = n.speakingStyle,
                        PersonalityTraits = n.personalityTraits,
                        DoRules = n.doRules,
                        DontRules = n.dontRules,
                        Capabilities = n.capabilities,
                        StartingTrust = n.startingTrust,
                        TrustToShareSecrets = n.trustToShareSecrets,
                        FallbackReply = n.fallbackReply,
                        DesignerNotesJson = notesJson,
                        ContentHash = hash,
                        Embedding = vec,
                    });
                    embedded++;
                }

            // ── Signals (no embedding) ──────────────────────────────────────────────
            if (content.signals != null)
                foreach (var s in content.signals)
                {
                    if (s == null || string.IsNullOrEmpty(s.signalId)) continue;
                    string hash = SceneLlmManager.ContentHash(islandId, "signal", s.signalId, s.thingId, s.description,
                        s.repeatable.ToString(), s.minTrustToFire.ToString(), s.handler, s.payloadJson);
                    var existing = db.Signals.FindOne(x => x.SignalId == s.signalId && x.IslandId == islandId);
                    if (existing != null && existing.ContentHash == hash) { skipped++; continue; }

                    db.Signals.Upsert(new SignalDoc
                    {
                        Id = existing != null ? existing.Id : 0,
                        IslandId = islandId,
                        SignalId = s.signalId,
                        ThingId = s.thingId,
                        Description = s.description,
                        Repeatable = s.repeatable,
                        MinTrustToFire = s.minTrustToFire,
                        Handler = s.handler,
                        PayloadJson = s.payloadJson,
                        ContentHash = hash,
                    });
                    skipped++; // counts as processed; not an embed
                }

            // ── Knowledge (npc + community) ─────────────────────────────────────────
            if (content.npcs != null)
                foreach (var n in content.npcs)
                {
                    if (n == null || n.knowledge == null) continue;
                    foreach (var k in n.knowledge)
                        yield return ImportKnowledge(db, embedder, islandId, n.npcId, k, r => { if (r) embedded++; else skipped++; });
                }
            if (community != null && community.knowledge != null)
                foreach (var k in community.knowledge)
                    yield return ImportKnowledge(db, embedder, islandId, "community", k, r => { if (r) embedded++; else skipped++; });

            Debug.Log($"[IslandDbImporter] Import complete for '{islandId}': {embedded} embedded/updated, {skipped} unchanged.");
        }

        private static IEnumerator ImportKnowledge(NpcMemoryDatabase db, OllamaEmbeddingProvider embedder,
            string islandId, string ownerScope, KnowledgeContent k, System.Action<bool> onDone)
        {
            if (k == null || string.IsNullOrWhiteSpace(k.text)) { onDone?.Invoke(false); yield break; }
            string hash = SceneLlmManager.ContentHash(islandId, ownerScope, k.id, k.thingId, k.kind, k.text, k.revealTrustThreshold.ToString());
            var existing = db.Knowledge.FindOne(x => x.ContentHash == hash);
            if (existing != null) { onDone?.Invoke(false); yield break; }

            // Find a prior row for this authored knowledge id (changed text) to update in place.
            var prior = !string.IsNullOrEmpty(k.id)
                ? db.Knowledge.FindOne(x => x.IslandId == islandId && x.OwnerScope == ownerScope && x.KnowledgeId == k.id)
                : null;

            float[] vec = null;
            yield return EmbedInto(embedder, k.text.Trim(), v => vec = v);
            if (vec == null) { onDone?.Invoke(false); yield break; }

            db.Knowledge.Upsert(new KnowledgeDoc
            {
                Id = prior != null ? prior.Id : 0,
                IslandId = islandId,
                KnowledgeId = k.id,
                OwnerScope = ownerScope,
                ThingId = k.thingId,
                Kind = k.kind,
                Text = k.text.Trim(),
                RevealTrust = k.revealTrustThreshold,
                ContentHash = hash,
                Embedding = vec,
            });
            onDone?.Invoke(true);
        }

        private static IEnumerator EmbedInto(OllamaEmbeddingProvider embedder, string text, System.Action<float[]> set)
        {
            if (string.IsNullOrWhiteSpace(text)) { set(null); yield break; }
            float[] vec = null;
            yield return embedder.Embed(text, (v, err) =>
            {
                if (err != null) Debug.LogWarning("[IslandDbImporter] Embed failed: " + err);
                else vec = v;
            });
            set(vec);
        }

        private static string Join(params string[] parts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(p.Trim());
            }
            return sb.ToString();
        }

        private static string Join(List<string> list)
        {
            if (list == null || list.Count == 0) return string.Empty;
            return string.Join(" ", list);
        }
    }

}
