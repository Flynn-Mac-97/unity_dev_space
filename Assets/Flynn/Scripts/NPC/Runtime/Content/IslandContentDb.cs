using System.Collections.Generic;
using UnityEngine;
using Flynn.Npc.Memory;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Npc
{
    // Conversion between the authored island POCOs (IslandContent) and the unified
    // LiteDB authored rows. Runtime (no UnityEditor dependency) so both the runtime
    // hub (DB → POCO at load) and the editor (POCO ↔ DB for authoring) share it.
    //
    // Embeddings are NOT produced here. The runtime importer (IslandDbImporter) owns
    // embedding for the play-time save DB; the design DB used by the editor needs no
    // vectors. ToContent ignores embeddings; SeedAuthored writes null vectors.
    public static class IslandContentDb
    {
        // Build the authored IslandContent for an island from its DB rows.
        public static IslandContent ToContent(NpcMemoryDatabase db, string islandId)
        {
            if (db == null || !db.IsOpen) return null;
            try
            {
            var content = new IslandContent { islandId = islandId };

            var communityDoc = db.Community.FindOne(x => x.IslandId == islandId);
            if (communityDoc != null)
            {
                content.community = new CommunityContent
                {
                    communityId = communityDoc.CommunityId,
                    displayName = communityDoc.DisplayName,
                    overview = communityDoc.Overview,
                    currentSituation = communityDoc.CurrentSituation,
                    sharedMood = communityDoc.SharedMood,
                    culture = communityDoc.Culture,
                    knowledge = KnowledgeFor(db, islandId, "community"),
                };
            }

            foreach (var t in db.Things.Find(x => x.IslandId == islandId))
                content.things.Add(new WorldThingContent
                {
                    thingId = t.ThingId,
                    displayName = t.DisplayName,
                    aliases = t.Aliases ?? new List<string>(),
                    tags = t.Tags ?? new List<string>(),
                    shortDescription = t.ShortDescription,
                });

            foreach (var s in db.Signals.Find(x => x.IslandId == islandId))
                content.signals.Add(new SignalContent
                {
                    signalId = s.SignalId,
                    thingId = s.ThingId,
                    description = s.Description,
                    repeatable = s.Repeatable,
                    minTrustToFire = s.MinTrustToFire,
                    handler = s.Handler,
                    payloadJson = s.PayloadJson,
                });

            foreach (var n in db.Npcs.Find(x => x.IslandId == islandId))
            {
                var npc = new NpcContent
                {
                    npcId = n.NpcId,
                    displayName = n.DisplayName,
                    role = n.Role,
                    speakingStyle = n.SpeakingStyle,
                    personalityTraits = n.PersonalityTraits ?? new List<string>(),
                    doRules = n.DoRules ?? new List<string>(),
                    dontRules = n.DontRules ?? new List<string>(),
                    capabilities = n.Capabilities ?? new List<string>(),
                    startingTrust = n.StartingTrust,
                    trustToShareSecrets = n.TrustToShareSecrets,
                    fallbackReply = n.FallbackReply,
                    knowledge = KnowledgeFor(db, islandId, n.NpcId),
                };
                if (!string.IsNullOrEmpty(n.DesignerNotesJson))
                {
                    try { npc.designerNotes = JsonUtility.FromJson<NpcDesignerNotes>(n.DesignerNotesJson); }
                    catch { /* notes are non-critical */ }
                }
                content.npcs.Add(npc);
            }

            return content;
            }
            catch (System.Exception e)
            {
                // The engine can be disposed mid-read if Play is stopped or scripts
                // recompile during the seed coroutine. Fail soft — the hub keeps its
                // JSON-parsed content rather than crashing the load.
                Debug.LogWarning("[IslandContentDb] ToContent aborted (DB disposed mid-read?): " + e.Message);
                return null;
            }
        }

        // Replace all authored rows for an island with the given content (no vectors).
        // Used by the editor's design DB. Dynamic collections are never touched.
        public static void SeedAuthored(NpcMemoryDatabase db, IslandContent content)
        {
            if (db == null || !db.IsOpen || content == null) return;
            string islandId = content.islandId ?? string.Empty;

            db.Community.DeleteMany(x => x.IslandId == islandId);
            db.Things.DeleteMany(x => x.IslandId == islandId);
            db.Npcs.DeleteMany(x => x.IslandId == islandId);
            db.Signals.DeleteMany(x => x.IslandId == islandId);
            db.Knowledge.DeleteMany(x => x.IslandId == islandId);

            if (content.community != null)
            {
                var c = content.community;
                db.Community.Insert(new CommunityDoc
                {
                    IslandId = islandId, CommunityId = c.communityId, DisplayName = c.displayName,
                    Overview = c.overview, CurrentSituation = c.currentSituation, SharedMood = c.sharedMood, Culture = c.culture,
                });
                InsertKnowledge(db, islandId, "community", c.knowledge);
            }

            if (content.things != null)
                foreach (var t in content.things)
                {
                    if (t == null || string.IsNullOrEmpty(t.thingId)) continue;
                    db.Things.Insert(new ThingDoc
                    {
                        IslandId = islandId, ThingId = t.thingId, DisplayName = t.displayName,
                        Aliases = t.aliases, Tags = t.tags, ShortDescription = t.shortDescription,
                    });
                }

            if (content.signals != null)
                foreach (var s in content.signals)
                {
                    if (s == null || string.IsNullOrEmpty(s.signalId)) continue;
                    db.Signals.Insert(new SignalDoc
                    {
                        IslandId = islandId, SignalId = s.signalId, ThingId = s.thingId, Description = s.description,
                        Repeatable = s.repeatable, MinTrustToFire = s.minTrustToFire, Handler = s.handler, PayloadJson = s.payloadJson,
                    });
                }

            if (content.npcs != null)
                foreach (var n in content.npcs)
                {
                    if (n == null || string.IsNullOrEmpty(n.npcId)) continue;
                    db.Npcs.Insert(new NpcDoc
                    {
                        IslandId = islandId, NpcId = n.npcId, DisplayName = n.displayName, Role = n.role,
                        SpeakingStyle = n.speakingStyle, PersonalityTraits = n.personalityTraits, DoRules = n.doRules,
                        DontRules = n.dontRules, Capabilities = n.capabilities, StartingTrust = n.startingTrust,
                        TrustToShareSecrets = n.trustToShareSecrets, FallbackReply = n.fallbackReply,
                        DesignerNotesJson = n.designerNotes != null ? JsonUtility.ToJson(n.designerNotes) : null,
                    });
                    InsertKnowledge(db, islandId, n.npcId, n.knowledge);
                }
        }

        private static void InsertKnowledge(NpcMemoryDatabase db, string islandId, string ownerScope, List<KnowledgeContent> list)
        {
            if (list == null) return;
            foreach (var k in list)
            {
                if (k == null || string.IsNullOrWhiteSpace(k.text)) continue;
                db.Knowledge.Insert(new KnowledgeDoc
                {
                    IslandId = islandId, KnowledgeId = k.id, OwnerScope = ownerScope, ThingId = k.thingId,
                    Kind = k.kind, Text = k.text, RevealTrust = k.revealTrustThreshold,
                });
            }
        }

        private static List<KnowledgeContent> KnowledgeFor(NpcMemoryDatabase db, string islandId, string ownerScope)
        {
            var list = new List<KnowledgeContent>();
            foreach (var k in db.Knowledge.Find(x => x.IslandId == islandId && x.OwnerScope == ownerScope))
                list.Add(new KnowledgeContent
                {
                    id = k.KnowledgeId, thingId = k.ThingId, kind = k.Kind, text = k.Text, revealTrustThreshold = k.RevealTrust,
                });
            return list;
        }
    }

}
