using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NpcDialogueAgentConfig))]
public class NpcDialogueAgentConfigEditor : Editor
{
    private SerializedProperty _personalityProfile;
    private SerializedProperty _promptTemplate;
    private SerializedProperty _useLocalModel;
    private SerializedProperty _fallbackReply;

    private void OnEnable()
    {
        _personalityProfile = serializedObject.FindProperty("personalityProfile");
        _promptTemplate = serializedObject.FindProperty("promptTemplate");
        _useLocalModel = serializedObject.FindProperty("useLocalModel");
        _fallbackReply = serializedObject.FindProperty("fallbackReply");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Single wiring point for one NPC dialogue agent. LLM model and memory budget are shared via SceneLlmManager.",
            MessageType.Info);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Asset References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_personalityProfile);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Prompt Template", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_promptTemplate);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_useLocalModel);
        EditorGUILayout.PropertyField(_fallbackReply);
        EditorGUILayout.EndVertical();

        if (_personalityProfile.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Missing personalityProfile reference.", MessageType.Warning);

        EditorGUILayout.Space();
        if (GUILayout.Button("Open NPC Crafting Studio"))
            NpcAuthoringStudioWindow.OpenWithConfig((NpcDialogueAgentConfig)target);

        serializedObject.ApplyModifiedProperties();
    }
}
