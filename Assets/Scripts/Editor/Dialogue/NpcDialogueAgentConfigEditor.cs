using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NpcDialogueAgentConfig))]
public class NpcDialogueAgentConfigEditor : Editor
{
    private SerializedProperty _personalityProfile;
    private SerializedProperty _promptTemplate;
    private SerializedProperty _memorySettings;
    private SerializedProperty _useLocalModel;
    private SerializedProperty _fallbackReply;

    private void OnEnable()
    {
        _personalityProfile = serializedObject.FindProperty("personalityProfile");
        _promptTemplate = serializedObject.FindProperty("promptTemplate");
        _memorySettings = serializedObject.FindProperty("memorySettings");
        _useLocalModel = serializedObject.FindProperty("useLocalModel");
        _fallbackReply = serializedObject.FindProperty("fallbackReply");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox("This asset is the single wiring point for one NPC dialogue agent.", MessageType.Info);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Asset References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_personalityProfile);
        EditorGUILayout.PropertyField(_promptTemplate);
        EditorGUILayout.PropertyField(_memorySettings);
        EditorGUILayout.EndVertical();

        var sceneManager = Object.FindObjectOfType<SceneLlmManager>();
        EditorGUILayout.HelpBox(
            sceneManager != null
                ? "Scene LLM manager found. NPCs will use shared model settings from that scene object."
                : "No SceneLlmManager found in open scenes. Add one to provide shared local model settings.",
            sceneManager != null ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_useLocalModel);
        EditorGUILayout.PropertyField(_fallbackReply);
        EditorGUILayout.EndVertical();

        DrawValidation();
        DrawPreviewShortcut();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawValidation()
    {
        if (_personalityProfile.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Missing personalityProfile reference.", MessageType.Warning);

        if (_promptTemplate.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Missing promptTemplate reference.", MessageType.Warning);

        if (_memorySettings.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Missing memorySettings reference.", MessageType.Warning);
    }

    private void DrawPreviewShortcut()
    {
        var template = _promptTemplate.objectReferenceValue as NpcPromptTemplate;
        var profile = _personalityProfile.objectReferenceValue as NpcPersonalityProfile;

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = template != null;
        if (GUILayout.Button("Open Prompt Preview"))
        {
            NpcPromptPreviewWindow.Open(template, profile);
        }
        GUI.enabled = true;

        if (GUILayout.Button("Open NPC Crafting Studio"))
        {
            NpcAuthoringStudioWindow.OpenWithConfig((NpcDialogueAgentConfig)target);
        }

        EditorGUILayout.EndHorizontal();
    }
}
