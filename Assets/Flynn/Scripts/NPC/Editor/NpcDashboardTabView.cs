using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class NpcDashboardTabView
{
    private Vector2 _scroll;

    private int _stubTrust = 50;
    private int _stubAffection = 50;
    private int _stubSuspicion = 20;
    private NpcRelationshipDefaults.PlayerStatusTag _stubStatus = NpcRelationshipDefaults.PlayerStatusTag.Useful;
    private string _stubActiveTopic = "(none)";

    public void Draw(NpcDialogueAgentConfig config)
    {
        if (config == null) return;

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSummary(config);
        DrawValidator(config);
        DrawPromptPreview(config);

        EditorGUILayout.EndScrollView();
    }

    private void DrawSummary(NpcDialogueAgentConfig config)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("SUMMARY", EditorStyles.boldLabel);

        var p = config.personalityProfile;
        EditorGUILayout.LabelField("Name", p != null ? p.GetSafeDisplayName() : "(none)");
        EditorGUILayout.LabelField("ID", p != null ? p.GetSafeNpcId() : "(none)");
        EditorGUILayout.LabelField("Roles", config.roles.ToString());
        EditorGUILayout.LabelField("Has knowledge", config.knowledge != null ? "yes" : "no");
        EditorGUILayout.LabelField("Has triggers", config.triggers != null ? "yes" : "no");
        EditorGUILayout.LabelField("Has relationship defaults", config.relationship != null ? "yes" : "no");

        EditorGUILayout.EndVertical();
    }

    private void DrawValidator(NpcDialogueAgentConfig config)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("VALIDATION", EditorStyles.boldLabel);

        var issues = NpcAuthoringValidator.Validate(config);
        EditorGUILayout.LabelField(NpcAuthoringValidator.FormatSummary(issues));

        for (int i = 0; i < issues.Count; i++)
        {
            var issue = issues[i];
            MessageType type;
            switch (issue.severity)
            {
                case NpcAuthoringValidator.Severity.Error: type = MessageType.Error; break;
                case NpcAuthoringValidator.Severity.Warning: type = MessageType.Warning; break;
                default: type = MessageType.Info; break;
            }
            EditorGUILayout.HelpBox(issue.message, type);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPromptPreview(NpcDialogueAgentConfig config)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("PROMPT PREVIEW", EditorStyles.boldLabel);

        if (config.promptTemplate == null)
        {
            EditorGUILayout.HelpBox("No prompt template assigned.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField("Stub runtime state — scrub to see how the prompt changes:", EditorStyles.miniLabel);
        _stubTrust = EditorGUILayout.IntSlider("Trust", _stubTrust, 0, 100);
        _stubAffection = EditorGUILayout.IntSlider("Affection", _stubAffection, 0, 100);
        _stubSuspicion = EditorGUILayout.IntSlider("Suspicion", _stubSuspicion, 0, 100);
        _stubStatus = (NpcRelationshipDefaults.PlayerStatusTag)EditorGUILayout.EnumPopup("Player status", _stubStatus);
        _stubActiveTopic = EditorGUILayout.TextField("Active topic", _stubActiveTopic);

        var ctx = new NpcPromptTemplate.PromptContext
        {
            trust = _stubTrust,
            affection = _stubAffection,
            suspicion = _stubSuspicion,
            playerStatus = _stubStatus,
            activeTopic = _stubActiveTopic,
            availableCluesBlock = BuildAvailableCluesBlock(config, _stubTrust),
            forbiddenTopicsLine = BuildForbiddenTopicsLine(config),
            roleFlagsLine = config.roles.ToString(),
            memorySummary = "- Player helped repair a wind pump yesterday.\n- NPC now trusts player with route advice.",
        };

        string assembled = config.promptTemplate.BuildAssembledPrompt(config.personalityProfile, ctx);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Assembled prompt:", EditorStyles.miniLabel);
        EditorGUILayout.SelectableLabel(assembled, EditorStyles.textArea, GUILayout.MinHeight(260f));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy prompt"))
            EditorGUIUtility.systemCopyBuffer = assembled;
        if (GUILayout.Button("Open floating preview"))
            NpcPromptPreviewWindow.Open(config.promptTemplate, config.personalityProfile);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private static string BuildAvailableCluesBlock(NpcDialogueAgentConfig config, int trust)
    {
        if (config.knowledge == null) return "(no knowledge authored)";

        int clueThreshold = config.relationship != null ? config.relationship.trustToShareClues : 50;
        int secretThreshold = config.relationship != null ? config.relationship.trustToShareSecrets : 75;

        var sb = new StringBuilder();
        AppendEntries(sb, config.knowledge.knownFacts, trust, 0, "fact");
        AppendEntries(sb, config.knowledge.beliefs, trust, 0, "belief");
        AppendEntries(sb, config.knowledge.rumors, trust, clueThreshold, "rumor");
        AppendEntries(sb, config.knowledge.secrets, trust, secretThreshold, "secret");

        if (sb.Length == 0) sb.Append("(nothing eligible at this relationship)");
        return sb.ToString().TrimEnd();
    }

    private static void AppendEntries(StringBuilder sb, List<NpcKnowledgeBase.KnowledgeEntry> entries, int trust, int defaultThreshold, string label)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || string.IsNullOrWhiteSpace(e.text)) continue;
            int needed = e.reveal == NpcKnowledgeBase.RevealCondition.TrustAtLeast ? e.threshold : defaultThreshold;
            if (trust < needed) continue;
            sb.Append("- [").Append(label).Append("] ");
            if (e.topic != null) sb.Append(e.topic.GetSafeDisplayName()).Append(": ");
            sb.Append(e.text.Trim()).Append('\n');
        }
    }

    private static string BuildForbiddenTopicsLine(NpcDialogueAgentConfig config)
    {
        if (config.knowledge == null || config.knowledge.avoidedTopics == null || config.knowledge.avoidedTopics.Count == 0)
            return "(none)";

        var sb = new StringBuilder();
        for (int i = 0; i < config.knowledge.avoidedTopics.Count; i++)
        {
            var t = config.knowledge.avoidedTopics[i];
            if (t == null) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(t.GetSafeDisplayName());
        }
        return sb.Length == 0 ? "(none)" : sb.ToString();
    }
}
