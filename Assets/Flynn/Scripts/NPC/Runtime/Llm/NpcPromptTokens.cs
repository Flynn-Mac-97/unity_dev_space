using System.Collections.Generic;
using System.Text;

// Token substitution for per-NPC system prompts. Designers write prose in
// NpcPersonalityProfile.systemPrompt and drop in {tokens} that get replaced
// at runtime with data from the other authoring tabs (knowledge, triggers,
// relationship). The NpcAuthoringStudio's Identity tab exposes a click-to-
// copy palette of these tokens.
public static class NpcPromptTokens
{
    public const string Name              = "{name}";
    public const string Role              = "{role}";
    public const string SpeakingStyle     = "{speaking_style}";
    public const string PersonalityTraits = "{personality_traits}";
    public const string DoRules           = "{do}";
    public const string DontRules         = "{dont}";
    public const string Capabilities      = "{capabilities}";
    public const string GameplayRoles     = "{gameplay_roles}";
    public const string KnownFacts        = "{known_facts}";
    public const string Beliefs           = "{beliefs}";
    public const string Rumors            = "{rumors}";
    public const string Secrets           = "{secrets}";
    public const string AvoidedTopics     = "{avoided_topics}";
    public const string Triggers          = "{triggers}";
    public const string Relationship      = "{relationship}";

    public static readonly string[] All =
    {
        Name, Role, SpeakingStyle, PersonalityTraits, DoRules, DontRules,
        Capabilities, GameplayRoles,
        KnownFacts, Beliefs, Rumors, Secrets, AvoidedTopics,
        Triggers, Relationship,
    };

    public static string Apply(string template, NpcDialogueAgentConfig config)
    {
        if (string.IsNullOrEmpty(template) || config == null) return template ?? string.Empty;

        var p = config.personalityProfile;
        var k = config.knowledge;
        var r = config.relationship;

        string s = template;
        s = s.Replace(Name,              p != null ? p.GetSafeDisplayName() : "the NPC");
        s = s.Replace(Role,              SafeText(p != null ? p.roleDescription : null));
        s = s.Replace(SpeakingStyle,     SafeText(p != null ? p.speakingStyle : null));
        s = s.Replace(PersonalityTraits, SafeText(p != null ? p.personalityTraits : null));
        s = s.Replace(DoRules,           SafeText(p != null ? p.doRules : null));
        s = s.Replace(DontRules,         SafeText(p != null ? p.dontRules : null));
        s = s.Replace(Capabilities,      FormatCapabilities(p != null ? p.capabilities : null));
        s = s.Replace(GameplayRoles,     config.roles.ToString());
        s = s.Replace(KnownFacts,        FormatEntries(k != null ? k.knownFacts : null));
        s = s.Replace(Beliefs,           FormatEntries(k != null ? k.beliefs : null));
        s = s.Replace(Rumors,            FormatEntries(k != null ? k.rumors : null));
        s = s.Replace(Secrets,           FormatEntries(k != null ? k.secrets : null));
        s = s.Replace(AvoidedTopics,     FormatAvoided(k != null ? k.avoidedTopics : null));
        s = s.Replace(Triggers,          FormatTriggers(config.triggers));
        s = s.Replace(Relationship,      FormatRelationship(r));
        return s;
    }

    private static string SafeText(string raw)
    {
        return string.IsNullOrWhiteSpace(raw) ? "(none)" : raw.Trim();
    }

    private static string FormatCapabilities(List<string> caps)
    {
        if (caps == null || caps.Count == 0) return "(none — do not promise in-game actions)";
        var sb = new StringBuilder();
        for (int i = 0; i < caps.Count; i++)
        {
            string c = caps[i];
            if (string.IsNullOrWhiteSpace(c)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("- ").Append(c.Trim());
        }
        return sb.Length == 0 ? "(none — do not promise in-game actions)" : sb.ToString();
    }

    private static string FormatEntries(List<NpcKnowledgeBase.KnowledgeEntry> entries)
    {
        if (entries == null || entries.Count == 0) return "(none)";
        var sb = new StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || string.IsNullOrWhiteSpace(e.text)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("- ");
            if (e.topic != null) sb.Append('[').Append(e.topic.GetSafeDisplayName()).Append("] ");
            sb.Append(e.text.Trim());
        }
        return sb.Length == 0 ? "(none)" : sb.ToString();
    }

    private static string FormatAvoided(List<Topic> topics)
    {
        if (topics == null || topics.Count == 0) return "(none)";
        var sb = new StringBuilder();
        for (int i = 0; i < topics.Count; i++)
        {
            if (topics[i] == null) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(topics[i].GetSafeDisplayName());
        }
        return sb.Length == 0 ? "(none)" : sb.ToString();
    }

    private static string FormatTriggers(List<DialogueTriggerDef> triggers)
    {
        if (triggers == null || triggers.Count == 0) return "(none)";
        var sb = new StringBuilder();
        for (int i = 0; i < triggers.Count; i++)
        {
            var t = triggers[i];
            if (t == null || t.draft) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("- ").Append(t.Key).Append(" (").Append(t.kind).Append("): ");
            sb.Append(string.IsNullOrWhiteSpace(t.description) ? "(no description)" : t.description.Trim());
        }
        return sb.Length == 0 ? "(none)" : sb.ToString();
    }

    private static string FormatRelationship(NpcRelationshipDefaults r)
    {
        if (r == null) return "(neutral)";
        return string.Format(
            "starting trust {0}, affection {1}, suspicion {2}; initial player status: {3}",
            r.startingTrust, r.startingAffection, r.startingSuspicion, r.initialPlayerStatus);
    }
}
