using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneLlmManager))]
public class SceneLlmManagerEditor : Editor
{
    private SerializedProperty _sharedLocalModelSettings;
    private SerializedProperty _llmEnabled;
    private SerializedProperty _saveSlotId;

    private void OnEnable()
    {
        _sharedLocalModelSettings = serializedObject.FindProperty("sharedLocalModelSettings");
        _llmEnabled = serializedObject.FindProperty("llmEnabled");
        _saveSlotId = serializedObject.FindProperty("saveSlotId");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox("Scene-level LLM hub. NPCs should read model settings from this manager instead of per-NPC config.", MessageType.Info);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Scene LLM", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_llmEnabled);
        EditorGUILayout.PropertyField(_sharedLocalModelSettings);
        EditorGUILayout.PropertyField(_saveSlotId);
        EditorGUILayout.EndVertical();

        DrawValidation();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawValidation()
    {
        if (_sharedLocalModelSettings.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign LocalModelSettings so all NPCs can share a model config.", MessageType.Warning);
        }

        if (!_llmEnabled.boolValue)
        {
            EditorGUILayout.HelpBox("LLM is disabled globally for this scene manager.", MessageType.Info);
        }

        if (string.IsNullOrWhiteSpace(_saveSlotId.stringValue))
        {
            EditorGUILayout.HelpBox("saveSlotId is empty. Memory will fall back to default slot_0.", MessageType.Info);
        }
    }
}
