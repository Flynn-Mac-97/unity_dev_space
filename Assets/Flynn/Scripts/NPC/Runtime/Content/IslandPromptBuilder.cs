using System.Text;


using Flynn.UI.Core;

namespace Flynn.Npc
{
    // Renders a ResolvedNpcContext into the per-turn system prompt block.
    // Replaces NpcPromptTokens.Apply(template, NpcData) — there is no more
    // templating; the structure is fixed and the data comes from JSON.
    public static class IslandPromptBuilder
    {
        public static string BuildPersonaBlock(ResolvedNpcContext ctx)
        {
            if (ctx == null || ctx.npc == null) return string.Empty;
            var n = ctx.npc;
            var sb = new StringBuilder();
            sb.Append("You are ").Append(SafeName(n.displayName)).Append(". ").Append(SafeText(n.role)).Append("\n\n");
            sb.Append("How you speak: ").Append(SafeText(n.speakingStyle)).Append('\n');
            sb.Append("Personality: ").Append(JoinList(n.personalityTraits, ", ")).Append("\n\n");
            sb.Append("Do:\n").Append(BulletList(n.doRules)).Append("\n\n");
            sb.Append("Don't:\n").Append(BulletList(n.dontRules)).Append("\n\n");
            sb.Append("Things you can do in this game:\n").Append(BulletList(n.capabilities)).Append("\n\n");
            sb.Append("Your relationship with the player: current trust ").Append(ctx.currentTrust).Append("/100 (started at ").Append(n.startingTrust).Append(").");
            return sb.ToString();
        }

        public static string BuildResolvedBlock(ResolvedNpcContext ctx)
        {
            if (ctx == null) return string.Empty;
            var sb = new StringBuilder();

            if (ctx.resolvedThing != null)
            {
                sb.Append("CURRENT DISCUSSED THING\n");
                sb.Append(ctx.resolvedThing.displayName);
                if (!string.IsNullOrWhiteSpace(ctx.resolvedThing.shortDescription))
                    sb.Append(" — ").Append(ctx.resolvedThing.shortDescription.Trim());
                if (!string.IsNullOrWhiteSpace(ctx.resolutionReason))
                    sb.Append("\n(Why selected: ").Append(ctx.resolutionReason).Append(')');
                sb.Append("\n\n");
            }

            if (ctx.npcKnowledge != null && ctx.npcKnowledge.Count > 0)
            {
                sb.Append("What you know:\n");
                for (int i = 0; i < ctx.npcKnowledge.Count; i++)
                {
                    var k = ctx.npcKnowledge[i];
                    sb.Append("- [").Append(SafeKind(k.kind)).Append("] ").Append(k.text.Trim()).Append('\n');
                }
                sb.Append('\n');
            }

            if (ctx.communityKnowledge != null && ctx.communityKnowledge.Count > 0)
            {
                sb.Append("What the community knows:\n");
                for (int i = 0; i < ctx.communityKnowledge.Count; i++)
                {
                    var k = ctx.communityKnowledge[i];
                    sb.Append("- ").Append(k.text.Trim()).Append('\n');
                }
                sb.Append('\n');
            }

            if (ctx.availableSignals != null && ctx.availableSignals.Count > 0)
            {
                sb.Append("Gameplay signals you may fire this turn:\n");
                for (int i = 0; i < ctx.availableSignals.Count; i++)
                {
                    var s = ctx.availableSignals[i];
                    sb.Append("- ").Append(s.signalId).Append(": ");
                    sb.Append(string.IsNullOrWhiteSpace(s.description) ? "(no description)" : s.description.Trim());
                    sb.Append('\n');
                }
                sb.Append("Only fire a signal if your reply clearly earns it. You MUST fire a signal when your reply matches its description — do not be conservative. Use the id verbatim in `triggers_fired`.");
            }

            if (!string.IsNullOrWhiteSpace(ctx.worldState))
            {
                sb.Append("\n\nWorld state right now:\n");
                sb.Append(ctx.worldState);
            }

            return sb.ToString().TrimEnd();
        }

        public static string BuildCommunityBlock(CommunityContent community)
        {
            if (community == null) return string.Empty;
            var sb = new StringBuilder();
            sb.Append("COMMUNITY: ").Append(SafeName(community.displayName)).Append('\n');
            if (!string.IsNullOrWhiteSpace(community.overview))         sb.Append(community.overview.Trim()).Append('\n');
            if (!string.IsNullOrWhiteSpace(community.currentSituation)) sb.Append("Right now: ").Append(community.currentSituation.Trim()).Append('\n');
            if (!string.IsNullOrWhiteSpace(community.sharedMood))       sb.Append("Mood: ").Append(community.sharedMood.Trim());
            return sb.ToString().TrimEnd();
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static string SafeName(string s) => string.IsNullOrWhiteSpace(s) ? "NPC" : s.Trim();
        private static string SafeText(string s) => string.IsNullOrWhiteSpace(s) ? "(none)" : s.Trim();
        private static string SafeKind(string s) => string.IsNullOrWhiteSpace(s) ? "FACT" : s.Trim().ToUpperInvariant();

        private static string JoinList(System.Collections.Generic.List<string> list, string sep)
        {
            if (list == null || list.Count == 0) return "(none)";
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(list[i])) continue;
                if (sb.Length > 0) sb.Append(sep);
                sb.Append(list[i].Trim());
            }
            return sb.Length == 0 ? "(none)" : sb.ToString();
        }

        private static string BulletList(System.Collections.Generic.List<string> list)
        {
            if (list == null || list.Count == 0) return "- (none)";
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(list[i])) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("- ").Append(list[i].Trim());
            }
            return sb.Length == 0 ? "- (none)" : sb.ToString();
        }
    }

}
