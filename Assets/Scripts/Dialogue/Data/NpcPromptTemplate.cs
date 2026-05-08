using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Dialogue/NPC Prompt Template")]
public class NpcPromptTemplate : ScriptableObject
{
    [Header("Core Prompt")]
    [TextArea(8, 16)]
    [FormerlySerializedAs("systemPreamble")]
    public string coreSystemPrompt =
        "You are {npc_name}, an NPC in this world.\n" +
        "Role: {role_description}\n" +
        "Style: {speaking_style}\n" +
        "Traits: {personality_traits}\n" +
        "Do: {do_rules}\n" +
        "Do Not: {dont_rules}\n" +
        "Reply in text only, 1-4 lines.";

    [Header("Memory Summary Section")]
    [TextArea(3, 8)]
    public string memorySummaryTemplate = "Memory summary:\n{memory_summary}";

    [Header("Optional")]
    [TextArea(2, 6)]
    public string optionalWorldContext = "";

    [SerializeField, HideInInspector, FormerlySerializedAs("personaSection")]
    private string _legacyPersonaSection = "";

    [SerializeField, HideInInspector, FormerlySerializedAs("rulesSection")]
    private string _legacyRulesSection = "";

    [SerializeField, HideInInspector, FormerlySerializedAs("memorySection")]
    private string _legacyMemorySection = "";

    [SerializeField, HideInInspector, FormerlySerializedAs("responseSection")]
    private string _legacyResponseSection = "";

    [SerializeField, HideInInspector, FormerlySerializedAs("worldContext")]
    private string _legacyWorldContext = "";

    public const string TokenNpcName = "{npc_name}";
    public const string TokenRoleDescription = "{role_description}";
    public const string TokenSpeakingStyle = "{speaking_style}";
    public const string TokenPersonalityTraits = "{personality_traits}";
    public const string TokenDoRules = "{do_rules}";
    public const string TokenDontRules = "{dont_rules}";
    public const string TokenMemorySummary = "{memory_summary}";

    public string BuildTemplate()
    {
        return BuildAssembledPrompt(null, TokenMemorySummary);
    }

    public string BuildAssembledPrompt(NpcPersonalityProfile profile, string memorySummary)
    {
        TryMigrateLegacyFields();

        string core = ReplaceProfileTokens(coreSystemPrompt, profile);
        string memory = BuildMemorySummarySection(memorySummary);

        return string.Join("\n\n", core.Trim(), memory.Trim(), optionalWorldContext.Trim()).Trim();
    }

    public string BuildMemorySummarySection(string memorySummary)
    {
        string safeSummary = string.IsNullOrWhiteSpace(memorySummary)
            ? "None yet."
            : memorySummary.Trim();

        return memorySummaryTemplate.Replace(TokenMemorySummary, safeSummary);
    }

    private static string ReplaceProfileTokens(string template, NpcPersonalityProfile profile)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        string npcName = profile != null ? profile.GetSafeDisplayName() : "NPC";
        string roleDescription = profile != null ? profile.roleDescription : "";
        string speakingStyle = profile != null ? profile.speakingStyle : "";
        string personalityTraits = profile != null ? profile.personalityTraits : "";
        string doRules = profile != null ? profile.doRules : "";
        string dontRules = profile != null ? profile.dontRules : "";

        return template
            .Replace(TokenNpcName, npcName)
            .Replace(TokenRoleDescription, roleDescription)
            .Replace(TokenSpeakingStyle, speakingStyle)
            .Replace(TokenPersonalityTraits, personalityTraits)
            .Replace(TokenDoRules, doRules)
            .Replace(TokenDontRules, dontRules);
    }

    private void OnValidate()
    {
        TryMigrateLegacyFields();
    }

    private void TryMigrateLegacyFields()
    {
        if (string.IsNullOrWhiteSpace(coreSystemPrompt))
            coreSystemPrompt = "";

        if (!string.IsNullOrWhiteSpace(_legacyPersonaSection)
            && coreSystemPrompt.IndexOf(_legacyPersonaSection, System.StringComparison.Ordinal) < 0)
            coreSystemPrompt = string.Join("\n\n", coreSystemPrompt, _legacyPersonaSection).Trim();

        if (!string.IsNullOrWhiteSpace(_legacyRulesSection)
            && coreSystemPrompt.IndexOf(_legacyRulesSection, System.StringComparison.Ordinal) < 0)
            coreSystemPrompt = string.Join("\n\n", coreSystemPrompt, _legacyRulesSection).Trim();

        if (!string.IsNullOrWhiteSpace(_legacyResponseSection)
            && coreSystemPrompt.IndexOf(_legacyResponseSection, System.StringComparison.Ordinal) < 0)
            coreSystemPrompt = string.Join("\n\n", coreSystemPrompt, _legacyResponseSection).Trim();

        if (!string.IsNullOrWhiteSpace(_legacyWorldContext))
            optionalWorldContext = _legacyWorldContext;

        if (string.IsNullOrWhiteSpace(memorySummaryTemplate) && !string.IsNullOrWhiteSpace(_legacyMemorySection))
            memorySummaryTemplate = _legacyMemorySection;

        if (!string.IsNullOrWhiteSpace(memorySummaryTemplate))
        {
            memorySummaryTemplate = memorySummaryTemplate
                .Replace("{memory_facts}", TokenMemorySummary)
                .Replace("{recent_turns}", TokenMemorySummary);
        }
    }
}
