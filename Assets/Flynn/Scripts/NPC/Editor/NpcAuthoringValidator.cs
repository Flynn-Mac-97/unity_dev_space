using System.Collections.Generic;
using System.Text;

public static class NpcAuthoringValidator
{
    public enum Severity { Info, Warning, Error }

    public struct Issue
    {
        public Severity severity;
        public string message;

        public Issue(Severity severity, string message)
        {
            this.severity = severity;
            this.message = message;
        }
    }

    public static List<Issue> Validate(NpcDialogueAgentConfig config)
    {
        var issues = new List<Issue>();
        if (config == null)
        {
            issues.Add(new Issue(Severity.Error, "No config assigned."));
            return issues;
        }

        if (config.personalityProfile == null)
            issues.Add(new Issue(Severity.Error, "Personality profile is missing."));
        else
            ValidateProfile(config.personalityProfile, issues);

        if (config.knowledge == null)
            issues.Add(new Issue(Severity.Warning, "No knowledge base. This NPC will have nothing to share."));
        else
            ValidateKnowledge(config, issues);

        if (config.triggers == null || config.triggers.Count == 0)
            issues.Add(new Issue(Severity.Info, "No dialogue triggers assigned. Triggers are optional but recommended for clue NPCs."));

        if (config.relationship == null)
            issues.Add(new Issue(Severity.Warning, "No relationship defaults. NPC will start at neutral trust/affection/suspicion."));

        ValidateRoles(config, issues);

        return issues;
    }

    private static void ValidateProfile(NpcPersonalityProfile profile, List<Issue> issues)
    {
        if (string.IsNullOrWhiteSpace(profile.npcId) || profile.npcId == "npc.stranger")
            issues.Add(new Issue(Severity.Warning, "Personality npcId is still the default. Give this NPC a unique id."));

        if (string.IsNullOrWhiteSpace(profile.displayName) || profile.displayName == "Stranger")
            issues.Add(new Issue(Severity.Warning, "Personality displayName is still the default."));

        if (profile.portraitSprite == null)
            issues.Add(new Issue(Severity.Info, "No portrait sprite assigned."));

        if (profile.inGameSprite == null)
            issues.Add(new Issue(Severity.Info, "No in-game sprite assigned."));

        if (string.IsNullOrWhiteSpace(profile.roleDescription))
            issues.Add(new Issue(Severity.Warning, "Role description is empty."));
    }

    private static void ValidateKnowledge(NpcDialogueAgentConfig config, List<Issue> issues)
    {
        var k = config.knowledge;
        int total = k.knownFacts.Count + k.beliefs.Count + k.rumors.Count + k.secrets.Count;

        if (total == 0)
            issues.Add(new Issue(Severity.Warning, "Knowledge base has no facts, beliefs, rumors, or secrets."));

        CheckTopics(k.knownFacts, "known fact", issues);
        CheckTopics(k.beliefs, "belief", issues);
        CheckTopics(k.rumors, "rumor", issues);
        CheckTopics(k.secrets, "secret", issues);

        for (int i = 0; i < k.avoidedTopics.Count; i++)
        {
            if (k.avoidedTopics[i] == null)
                issues.Add(new Issue(Severity.Warning, "Avoided topic entry " + i + " has no Topic asset assigned."));
        }
    }

    private static void CheckTopics(List<NpcKnowledgeBase.KnowledgeEntry> entries, string label, List<Issue> issues)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null) continue;

            if (entry.topic == null)
                issues.Add(new Issue(Severity.Info, "A " + label + " entry has no Topic assigned (entry " + i + ")."));

            if (string.IsNullOrWhiteSpace(entry.text))
                issues.Add(new Issue(Severity.Warning, "A " + label + " entry has no text (entry " + i + ")."));

            if (entry.reveal == NpcKnowledgeBase.RevealCondition.FlagSet && string.IsNullOrWhiteSpace(entry.flagKey))
                issues.Add(new Issue(Severity.Warning, "A " + label + " is gated by FlagSet but has no flag key (entry " + i + ")."));
        }
    }

    private static void ValidateRoles(NpcDialogueAgentConfig config, List<Issue> issues)
    {
        var roles = config.roles;

        if (roles == NpcGameplayRoles.None)
            issues.Add(new Issue(Severity.Info, "No gameplay roles set. Tag at least one role so the validator can check coverage."));

        if ((roles & NpcGameplayRoles.ClueGiver) != 0)
        {
            bool hasClue = config.knowledge != null && (config.knowledge.secrets.Count > 0 || config.knowledge.rumors.Count > 0);
            bool hasClueTrigger = HasTriggerOfKind(config, DialogueTriggerDef.TriggerKind.ClueReveal)
                                || HasTriggerOfKind(config, DialogueTriggerDef.TriggerKind.Secret);

            if (!hasClue && !hasClueTrigger)
                issues.Add(new Issue(Severity.Warning, "Tagged ClueGiver but has no rumors, secrets, or clue triggers authored."));
        }

        if ((roles & NpcGameplayRoles.LoreKeeper) != 0)
        {
            bool hasLore = config.knowledge != null && config.knowledge.knownFacts.Count > 0;
            if (!hasLore)
                issues.Add(new Issue(Severity.Info, "Tagged LoreKeeper but has no known facts authored."));
        }

        if ((roles & NpcGameplayRoles.Merchant) != 0)
            issues.Add(new Issue(Severity.Info, "Trade profile is a v2 feature. Merchant role will be inert until that ships."));
    }

    private static bool HasTriggerOfKind(NpcDialogueAgentConfig config, DialogueTriggerDef.TriggerKind kind)
    {
        if (config.triggers == null) return false;
        for (int i = 0; i < config.triggers.Count; i++)
        {
            var t = config.triggers[i];
            if (t != null && t.kind == kind && !t.draft) return true;
        }
        return false;
    }

    public static string FormatSummary(List<Issue> issues)
    {
        if (issues == null || issues.Count == 0)
            return "All checks passed.";

        int errors = 0, warnings = 0, infos = 0;
        for (int i = 0; i < issues.Count; i++)
        {
            switch (issues[i].severity)
            {
                case Severity.Error: errors++; break;
                case Severity.Warning: warnings++; break;
                case Severity.Info: infos++; break;
            }
        }

        var sb = new StringBuilder();
        sb.Append(errors).Append(" error");
        if (errors != 1) sb.Append('s');
        sb.Append(", ").Append(warnings).Append(" warning");
        if (warnings != 1) sb.Append('s');
        sb.Append(", ").Append(infos).Append(" info.");
        return sb.ToString();
    }
}
