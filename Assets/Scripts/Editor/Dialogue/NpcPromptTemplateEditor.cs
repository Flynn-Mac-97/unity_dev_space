using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NpcPromptTemplate))]
public class NpcPromptTemplateEditor : Editor
{
    private static readonly string[] Tokens =
    {
        NpcPromptTemplate.TokenNpcName,
        NpcPromptTemplate.TokenRoleDescription,
        NpcPromptTemplate.TokenSpeakingStyle,
        NpcPromptTemplate.TokenPersonalityTraits,
        NpcPromptTemplate.TokenDoRules,
        NpcPromptTemplate.TokenDontRules,
        NpcPromptTemplate.TokenMemorySummary
    };

    private SerializedProperty _coreSystemPrompt;
    private SerializedProperty _memorySummaryTemplate;
    private SerializedProperty _optionalWorldContext;

    private void OnEnable()
    {
        _coreSystemPrompt = serializedObject.FindProperty("coreSystemPrompt");
        _memorySummaryTemplate = serializedObject.FindProperty("memorySummaryTemplate");
        _optionalWorldContext = serializedObject.FindProperty("optionalWorldContext");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox("Use one core system prompt plus a memory summary block. Keep it deterministic and text-only.", MessageType.Info);

        DrawTokenHelperButtons();

        EditorGUILayout.LabelField("Core Prompt", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_coreSystemPrompt, GUIContent.none);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Memory Summary Template", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_memorySummaryTemplate, GUIContent.none);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional World Context", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_optionalWorldContext, GUIContent.none);

        DrawActions();

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawTokenHelperButtons()
    {
        EditorGUILayout.LabelField("Token Palette", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Click a token to copy it to clipboard.", EditorStyles.miniLabel);

        int perRow = 3;
        for (int i = 0; i < Tokens.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < i + perRow && j < Tokens.Length; j++)
            {
                if (GUILayout.Button(Tokens[j]))
                {
                    EditorGUIUtility.systemCopyBuffer = Tokens[j];
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Open Prompt Preview", GUILayout.Height(24f)))
        {
            NpcPromptPreviewWindow.Open((NpcPromptTemplate)target);
        }

        if (GUILayout.Button("Open NPC Crafting Studio", GUILayout.Height(24f)))
        {
            NpcAuthoringStudioWindow.OpenWithTemplate((NpcPromptTemplate)target);
        }
        EditorGUILayout.EndHorizontal();
    }
}
