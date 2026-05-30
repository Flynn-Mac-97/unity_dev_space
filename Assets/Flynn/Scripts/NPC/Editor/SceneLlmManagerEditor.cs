using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneLlmManager))]
public class SceneLlmManagerEditor : Editor
{
    private SerializedProperty _sharedLocalModelSettings;
    private SerializedProperty _sharedMemorySettings;
    private SerializedProperty _capabilityLibrary;
    private SerializedProperty _llmEnabled;
    private SerializedProperty _saveSlotId;
    private SerializedProperty _systemPrompt;
    private SerializedProperty _playerProfile;

    private SerializedObject _memorySo;

    private void OnEnable()
    {
        _sharedLocalModelSettings = serializedObject.FindProperty("sharedLocalModelSettings");
        _sharedMemorySettings = serializedObject.FindProperty("sharedMemorySettings");
        _capabilityLibrary = serializedObject.FindProperty("capabilityLibrary");
        _llmEnabled = serializedObject.FindProperty("llmEnabled");
        _saveSlotId = serializedObject.FindProperty("saveSlotId");
        _systemPrompt = serializedObject.FindProperty("systemPrompt");
        _playerProfile = serializedObject.FindProperty("playerProfile");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Scene-level LLM hub. Every NPC in this scene shares these settings — model, memory budget, global system prompt, save slot, and player profile.",
            MessageType.Info);

        Section("LLM");
        EditorGUILayout.PropertyField(_llmEnabled);
        EditorGUILayout.PropertyField(_sharedLocalModelSettings);
        if (_sharedLocalModelSettings.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Assign LocalModelSettings so all NPCs can share a model config.", MessageType.Warning);

        Section("Global System Prompt");
        EditorGUILayout.HelpBox("Prepended to every NPC's prompt template. Use this for global rules (response length, never break character, world framing).", MessageType.None);
        EditorGUILayout.PropertyField(_systemPrompt, GUIContent.none);

        Section("Player");
        EditorGUILayout.PropertyField(_playerProfile);

        Section("Shared Memory Settings");
        EditorGUILayout.PropertyField(_sharedMemorySettings);
        if (_sharedMemorySettings.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("No NpcMemorySettings asset assigned. Memory limits will fall back to code defaults.", MessageType.Warning);
        }
        else
        {
            var memTarget = _sharedMemorySettings.objectReferenceValue;
            if (_memorySo == null || _memorySo.targetObject != memTarget)
                _memorySo = new SerializedObject(memTarget);

            _memorySo.Update();
            EditorGUILayout.PropertyField(_memorySo.FindProperty("recentTurnsLimit"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("memoryFactsLimit"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("maxFactLength"));
            _memorySo.ApplyModifiedProperties();
        }

        Section("Capability Library");
        EditorGUILayout.PropertyField(_capabilityLibrary);
        if (_capabilityLibrary.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Assign or create an NpcCapabilityLibrary so designers can pick capabilities per NPC.", MessageType.Info);

        Section("Save Context");
        EditorGUILayout.PropertyField(_saveSlotId);
        if (string.IsNullOrWhiteSpace(_saveSlotId.stringValue))
            EditorGUILayout.HelpBox("saveSlotId is empty. Memory will fall back to default slot_0.", MessageType.Info);

        if (!_llmEnabled.boolValue)
            EditorGUILayout.HelpBox("LLM is disabled globally for this scene manager.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
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
