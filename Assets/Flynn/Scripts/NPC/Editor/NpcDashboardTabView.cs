using UnityEditor;
using UnityEngine;

public class NpcDashboardTabView
{
    private Vector2 _scroll;
    private SerializedObject _configSo;
    private Vector2 _previewScroll;
    private Vector2 _templateScroll;

    private const float TemplateBoxHeight = 220f;
    private const float PreviewBoxHeight  = 260f;

    public void Draw(NpcDialogueAgentConfig config)
    {
        if (config == null) return;

        if (_configSo == null || _configSo.targetObject != config)
            _configSo = new SerializedObject(config);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSummary(config);
        DrawValidator(config);
        DrawPromptBuilder(config);
        DrawRuntime();

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

    private void DrawPromptBuilder(NpcDialogueAgentConfig config)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("PROMPT BUILDER", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Author the per-NPC system prompt below. {tokens} are substituted at runtime with data from the Identity, Knowledge, Triggers, and Relationships tabs. The global system prompt on SceneLlmManager is prepended automatically.",
            MessageType.Info);

        DrawTokenPalette();

        _configSo.Update();
        var templateProp = _configSo.FindProperty("promptTemplate");
        EditorGUILayout.LabelField("Template", EditorStyles.miniBoldLabel);
        _templateScroll = EditorGUILayout.BeginScrollView(
            _templateScroll, GUILayout.Height(TemplateBoxHeight));
        templateProp.stringValue = EditorGUILayout.TextArea(
            templateProp.stringValue, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        _configSo.ApplyModifiedProperties();

        EditorGUILayout.Space(6f);
        DrawPreview(config);

        EditorGUILayout.EndVertical();
    }

    private static void DrawTokenPalette()
    {
        EditorGUILayout.LabelField("Tokens (click to copy)", EditorStyles.miniBoldLabel);
        const int perRow = 4;
        for (int i = 0; i < NpcPromptTokens.All.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < i + perRow && j < NpcPromptTokens.All.Length; j++)
            {
                string token = NpcPromptTokens.All[j];
                if (GUILayout.Button(token))
                    EditorGUIUtility.systemCopyBuffer = token;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space(4f);
    }

    private void DrawPreview(NpcDialogueAgentConfig config)
    {
        EditorGUILayout.LabelField("Preview (what the LLM will see)", EditorStyles.miniBoldLabel);

        var sceneLlm = Object.FindObjectOfType<SceneLlmManager>();

        var sb = new System.Text.StringBuilder();
        if (sceneLlm != null && !string.IsNullOrWhiteSpace(sceneLlm.systemPrompt))
            sb.Append(sceneLlm.systemPrompt.Trim());

        if (!string.IsNullOrWhiteSpace(config.promptTemplate))
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(NpcPromptTokens.Apply(config.promptTemplate.Trim(), config));
        }

        string assembled = sb.Length > 0 ? sb.ToString() : "(no prompt configured)";

        if (sceneLlm == null)
            EditorGUILayout.HelpBox("No SceneLlmManager in open scenes — global prompt section is empty in this preview.", MessageType.Info);

        _previewScroll = EditorGUILayout.BeginScrollView(
            _previewScroll, GUILayout.Height(PreviewBoxHeight));

        // SelectableLabel doesn't grow to fit content, so measure the assembled string
        // against the current view width and give the label an explicit height. That
        // makes the surrounding fixed-height scroll view actually scroll.
        float viewWidth = EditorGUIUtility.currentViewWidth - 60f;
        float contentHeight = EditorStyles.textArea.CalcHeight(
            new GUIContent(assembled), Mathf.Max(120f, viewWidth));
        EditorGUILayout.SelectableLabel(assembled, EditorStyles.textArea,
            GUILayout.Height(contentHeight), GUILayout.ExpandWidth(true));

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Copy preview"))
            EditorGUIUtility.systemCopyBuffer = assembled;
    }

    private void DrawRuntime()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("RUNTIME", EditorStyles.boldLabel);
        _configSo.Update();
        EditorGUILayout.PropertyField(_configSo.FindProperty("useLocalModel"));
        EditorGUILayout.PropertyField(_configSo.FindProperty("fallbackReply"));
        _configSo.ApplyModifiedProperties();
        EditorGUILayout.EndVertical();
    }
}
