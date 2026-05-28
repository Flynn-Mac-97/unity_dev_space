using System.Collections.Generic;
using System.Text;

public static class NpcPromptContextBuilder
{
    public static NpcPromptTemplate.PromptContext Build(
        NpcDialogueAgentConfig config,
        NpcRelationshipState liveRelationship,
        string memorySummary)
    {
        var ctx = new NpcPromptTemplate.PromptContext();

        ApplyRelationship(ref ctx, config, liveRelationship);
        ctx.activeTopic = "(none)";
        ctx.availableCluesBlock = BuildCluesBlock(config, ctx.trust, ctx.affection, ctx.suspicion);
        ctx.forbiddenTopicsLine = BuildForbiddenTopicsLine(config);
        ctx.roleFlagsLine = BuildRoleFlagsLine(config);
        ctx.memorySummary = memorySummary;

        return ctx;
    }

    private static void ApplyRelationship(
        ref NpcPromptTemplate.PromptContext ctx,
        NpcDialogueAgentConfig config,
        NpcRelationshipState live)
    {
        if (live != null)
        {
            ctx.trust = live.trust;
            ctx.affection = live.affection;
            ctx.suspicion = live.suspicion;
            ctx.playerStatus = live.playerStatus;
            return;
        }

        if (config != null && config.relationship != null)
        {
            var d = config.relationship;
            ctx.trust = d.startingTrust;
            ctx.affection = d.startingAffection;
            ctx.suspicion = d.startingSuspicion;
            ctx.playerStatus = d.initialPlayerStatus;
            return;
        }

        ctx.trust = 25;
        ctx.affection = 25;
        ctx.suspicion = 25;
        ctx.playerStatus = NpcRelationshipDefaults.PlayerStatusTag.Unknown;
    }

    private static string BuildCluesBlock(NpcDialogueAgentConfig config, int trust, int affection, int suspicion)
    {
        if (config == null || config.knowledge == null) return "(none)";

        var sb = new StringBuilder();
        AppendBucket(sb, "Known facts", config.knowledge.knownFacts, trust, affection, suspicion);
        AppendBucket(sb, "Beliefs", config.knowledge.beliefs, trust, affection, suspicion);
        AppendBucket(sb, "Rumors you trust", config.knowledge.rumors, trust, affection, suspicion);
        AppendBucket(sb, "Secrets you may share now", config.knowledge.secrets, trust, affection, suspicion);

        string result = sb.ToString().TrimEnd();
        return string.IsNullOrEmpty(result) ? "(none)" : result;
    }

    private static void AppendBucket(
        StringBuilder sb, string header,
        List<NpcKnowledgeBase.KnowledgeEntry> entries,
        int trust, int affection, int suspicion)
    {
        if (entries == null || entries.Count == 0) return;

        bool any = false;
        var pending = new StringBuilder();

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (!MeetsReveal(e, trust, affection, suspicion)) continue;
            if (string.IsNullOrWhiteSpace(e.text)) continue;

            string topicName = e.topic != null ? e.topic.GetSafeDisplayName() : "general";
            pending.Append("- [").Append(topicName).Append("] ").Append(e.text.Trim()).Append('\n');
            any = true;
        }

        if (!any) return;
        sb.Append(header).Append(":\n").Append(pending).Append('\n');
    }

    private static bool MeetsReveal(NpcKnowledgeBase.KnowledgeEntry e, int trust, int affection, int suspicion)
    {
        switch (e.reveal)
        {
            case NpcKnowledgeBase.RevealCondition.Always: return true;
            case NpcKnowledgeBase.RevealCondition.TrustAtLeast: return trust >= e.threshold;
            case NpcKnowledgeBase.RevealCondition.AffectionAtLeast: return affection >= e.threshold;
            case NpcKnowledgeBase.RevealCondition.SuspicionAtMost: return suspicion <= e.threshold;
            case NpcKnowledgeBase.RevealCondition.FlagSet: return false;
            default: return false;
        }
    }

    private static string BuildForbiddenTopicsLine(NpcDialogueAgentConfig config)
    {
        if (config == null || config.knowledge == null || config.knowledge.avoidedTopics == null) return "(none)";

        var names = new List<string>();
        for (int i = 0; i < config.knowledge.avoidedTopics.Count; i++)
        {
            var t = config.knowledge.avoidedTopics[i];
            if (t == null) continue;
            names.Add(t.GetSafeDisplayName());
        }

        return names.Count == 0 ? "(none)" : string.Join(", ", names);
    }

    private static string BuildRoleFlagsLine(NpcDialogueAgentConfig config)
    {
        if (config == null || config.roles == NpcGameplayRoles.None) return "(none)";

        var parts = new List<string>();
        foreach (NpcGameplayRoles v in System.Enum.GetValues(typeof(NpcGameplayRoles)))
        {
            if (v == NpcGameplayRoles.None) continue;
            if ((config.roles & v) == v) parts.Add(v.ToString());
        }

        return parts.Count == 0 ? "(none)" : string.Join(", ", parts);
    }
}
