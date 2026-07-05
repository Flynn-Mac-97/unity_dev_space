using System;
using System.Collections.Generic;
using UnityEngine;


using Flynn.UI.Core;

namespace Flynn.Npc
{
    // One slice of authored island content selected for a single dialogue turn.
    // The prompt builder consumes this — the LLM never sees the full island.
    public class ResolvedNpcContext
    {
        public string npcId;
        public NpcContent npc;

        // Runtime trust at the time of resolution, passed to the prompt builder
        // so the LLM sees the real current trust, not just the starting value.
        public int currentTrust;

        // Human-readable world state snapshot (power level, solar collectors, relay).
        // Populated by DialogueManager before the prompt is built.
        public string worldState;

        public string resolvedThingId;        // empty when nothing matched
        public WorldThingContent resolvedThing;
        public string resolutionReason;       // human-readable, included in the prompt for debugging

        public List<KnowledgeContent> npcKnowledge = new List<KnowledgeContent>();
        public List<KnowledgeContent> communityKnowledge = new List<KnowledgeContent>();
        public List<SignalContent> availableSignals = new List<SignalContent>();
    }

    // Picks the relevant slice of island content for one turn. Stateless beyond
    // the hub reference; safe to call every turn. Resolution order:
    //   1. explicit `currentThingId` (player just interacted with a WorldThingLink)
    //   2. nearest WorldThingLink within `proximityRadius` of `playerPosition`
    //   3. alias match against player input
    // Anything not found is fine — the prompt still goes out, just without a
    // resolved-thing block. Knowledge entries respect trust; signals tied to a
    // resolved thing are surfaced alongside `thingId == ""` signals (untied,
    // e.g. first-meeting).
    public class NpcContextResolver
    {
        private readonly IslandContentHub _hub;

        public NpcContextResolver(IslandContentHub hub) { _hub = hub; }

        public ResolvedNpcContext Resolve(
            string npcId,
            int currentTrust,
            string playerInput,
            string currentThingId,
            Vector3? playerPosition,
            float proximityRadius)
        {
            var ctx = new ResolvedNpcContext { npcId = npcId, currentTrust = currentTrust };
            if (_hub == null || !_hub.IsLoaded)
            {
                ctx.resolutionReason = "no island content loaded";
                return ctx;
            }

            ctx.npc = _hub.GetNpc(npcId);

            WorldThingContent thing = null;
            string reason = null;

            if (!string.IsNullOrEmpty(currentThingId))
            {
                thing = _hub.GetThing(currentThingId);
                if (thing != null) reason = "explicit current thing";
            }

            if (thing == null && playerPosition.HasValue && proximityRadius > 0f)
            {
                float bestSqr = proximityRadius * proximityRadius;
                WorldThingLink bestLink = null;
                var all = WorldThingLink.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var link = all[i];
                    if (link == null || string.IsNullOrEmpty(link.thingId)) continue;
                    float sqr = (link.transform.position - playerPosition.Value).sqrMagnitude;
                    if (sqr <= bestSqr) { bestSqr = sqr; bestLink = link; }
                }
                if (bestLink != null)
                {
                    thing = _hub.GetThing(bestLink.thingId);
                    if (thing != null) reason = "player is near " + thing.displayName;
                }
            }

            if (thing == null && !string.IsNullOrWhiteSpace(playerInput))
            {
                thing = MatchByAliasInText(playerInput);
                if (thing != null) reason = "player mentioned '" + thing.displayName + "'";
            }

            if (thing != null)
            {
                ctx.resolvedThing = thing;
                ctx.resolvedThingId = thing.thingId;
                ctx.resolutionReason = reason;
            }

            // Knowledge for the resolved thing (if any) + always-on entries (thingId empty)
            if (ctx.npc != null)
                CollectKnowledge(ctx.npc.knowledge, ctx.resolvedThingId, currentTrust, ctx.npcKnowledge);

            var community = _hub.GetCommunity();
            if (community != null)
                CollectKnowledge(community.knowledge, ctx.resolvedThingId, currentTrust, ctx.communityKnowledge);

            // Signals tied to the resolved thing + untied (empty-thingId) signals.
            // Trust gate applied; repeatability and already-fired checks happen in SignalHub.
            AddSignals(ctx.availableSignals, _hub.GetSignalsForThing(string.Empty), currentTrust);
            if (!string.IsNullOrEmpty(ctx.resolvedThingId))
                AddSignals(ctx.availableSignals, _hub.GetSignalsForThing(ctx.resolvedThingId), currentTrust);

            return ctx;
        }

        private WorldThingContent MatchByAliasInText(string input)
        {
            if (_hub.Content == null || _hub.Content.things == null) return null;
            string lower = input.ToLowerInvariant();
            WorldThingContent best = null;
            int bestLen = 0;
            foreach (var t in _hub.Content.things)
            {
                if (t == null) continue;
                if (TryMatchAlias(lower, t.displayName, ref best, ref bestLen, t)) { }
                if (t.aliases != null)
                    foreach (var alias in t.aliases)
                        TryMatchAlias(lower, alias, ref best, ref bestLen, t);
            }
            return best;
        }

        private static bool TryMatchAlias(string lowerInput, string alias, ref WorldThingContent best, ref int bestLen, WorldThingContent candidate)
        {
            if (string.IsNullOrWhiteSpace(alias)) return false;
            string a = alias.Trim().ToLowerInvariant();
            if (a.Length <= bestLen) return false;
            if (lowerInput.IndexOf(a, StringComparison.Ordinal) < 0) return false;
            best = candidate;
            bestLen = a.Length;
            return true;
        }

        private static void CollectKnowledge(List<KnowledgeContent> source, string thingId, int trust, List<KnowledgeContent> dest)
        {
            if (source == null) return;
            foreach (var k in source)
            {
                if (k == null || string.IsNullOrWhiteSpace(k.text)) continue;
                // Always include untied knowledge (no thingId). Otherwise only matches resolved thing.
                bool untied = string.IsNullOrEmpty(k.thingId);
                if (!untied && !string.Equals(k.thingId, thingId, StringComparison.Ordinal)) continue;
                if (string.IsNullOrEmpty(thingId) && !untied) continue; // skip thing-bound when no thing resolved
                if (k.revealTrustThreshold > 0 && trust < k.revealTrustThreshold) continue;
                dest.Add(k);
            }
        }

        private static void AddSignals(List<SignalContent> dest, IReadOnlyList<SignalContent> source, int trust)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                var s = source[i];
                if (s == null || string.IsNullOrEmpty(s.signalId)) continue;
                if (s.minTrustToFire > 0 && trust < s.minTrustToFire) continue;
                dest.Add(s);
            }
        }
    }

}
