using UnityEditor;
using UnityEngine;

public class NpcAiTabView
{
    private Vector2 _scroll;
    private SerializedObject _templateSo;
    private SerializedObject _memorySo;
    private SerializedObject _configSo;
    private SerializedObject _sceneLlmManagerSo;
    private SceneLlmManager _sceneLlmManager;

    public void Draw(NpcDialogueAgentConfig config)
    {
        if (config == null) return;

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (config.promptTemplate == null)
            EditorGUILayout.HelpBox("No prompt template assigned. Click Save to auto-create one.", MessageType.Warning);
        else
        {
            if (_templateSo == null || _templateSo.targetObject != config.promptTemplate)
                _templateSo = new SerializedObject(config.promptTemplate);

            Section("Prompt template");
            _templateSo.Update();
            EditorGUILayout.PropertyField(_templateSo.FindProperty("coreSystemPrompt"));
            EditorGUILayout.PropertyField(_templateSo.FindProperty("memorySummaryTemplate"));
            EditorGUILayout.PropertyField(_templateSo.FindProperty("optionalWorldContext"));
            _templateSo.ApplyModifiedProperties();

            DrawTokenPalette();
        }

        if (config.memorySettings != null)
        {
            if (_memorySo == null || _memorySo.targetObject != config.memorySettings)
                _memorySo = new SerializedObject(config.memorySettings);

            Section("Memory budget");
            _memorySo.Update();
            EditorGUILayout.PropertyField(_memorySo.FindProperty("recentTurnsLimit"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("memoryFactsLimit"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("maxFactLength"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("injectedRecentTurns"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("injectedFacts"));
            _memorySo.ApplyModifiedProperties();
        }

        if (_configSo == null || _configSo.targetObject != config)
            _configSo = new SerializedObject(config);

        Section("Runtime");
        _configSo.Update();
        EditorGUILayout.PropertyField(_configSo.FindProperty("useLocalModel"));
        EditorGUILayout.PropertyField(_configSo.FindProperty("fallbackReply"));
        _configSo.ApplyModifiedProperties();

        Section("Scene LLM manager");
        if (_sceneLlmManager == null)
            _sceneLlmManager = Object.FindObjectOfType<SceneLlmManager>();

        _sceneLlmManager = (SceneLlmManager)EditorGUILayout.ObjectField("Manager", _sceneLlmManager, typeof(SceneLlmManager), true);
        if (_sceneLlmManager != null)
        {
            if (_sceneLlmManagerSo == null || _sceneLlmManagerSo.targetObject != _sceneLlmManager)
                _sceneLlmManagerSo = new SerializedObject(_sceneLlmManager);
            _sceneLlmManagerSo.Update();
            EditorGUILayout.PropertyField(_sceneLlmManagerSo.FindProperty("llmEnabled"));
            EditorGUILayout.PropertyField(_sceneLlmManagerSo.FindProperty("sharedLocalModelSettings"));
            EditorGUILayout.PropertyField(_sceneLlmManagerSo.FindProperty("saveSlotId"));
            _sceneLlmManagerSo.ApplyModifiedProperties();
        }
        else
        {
            EditorGUILayout.HelpBox("No SceneLlmManager in the open scenes. Add one so every NPC shares the same model.", MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawTokenPalette()
    {
        Section("Token palette");
        EditorGUILayout.LabelField("Click a token to copy it into your clipboard.", EditorStyles.miniLabel);
        Row(NpcPromptTemplate.TokenNpcName, NpcPromptTemplate.TokenRoleDescription, NpcPromptTemplate.TokenSpeakingStyle);
        Row(NpcPromptTemplate.TokenPersonalityTraits, NpcPromptTemplate.TokenDoRules, NpcPromptTemplate.TokenDontRules);
        Row(NpcPromptTemplate.TokenRelationshipSummary, NpcPromptTemplate.TokenActiveTopic, NpcPromptTemplate.TokenRoleFlags);
        Row(NpcPromptTemplate.TokenAvailableClues, NpcPromptTemplate.TokenForbiddenTopics, NpcPromptTemplate.TokenMemorySummary);
    }

    private static void Row(params string[] tokens)
    {
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < tokens.Length; i++)
        {
            if (GUILayout.Button(tokens[i]))
                EditorGUIUtility.systemCopyBuffer = tokens[i];
        }
        EditorGUILayout.EndHorizontal();
    }

    private static void Section(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title.ToUpperInvariant(), EditorStyles.boldLabel);
        var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.3f));
        EditorGUILayout.Space(2f);
    }
}
