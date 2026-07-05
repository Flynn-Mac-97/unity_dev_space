using UnityEditor;
using UnityEngine;


using Flynn.Core;
using Flynn.Npc;
using Flynn.Npc.Memory;
using Flynn.UI.Core;

namespace Flynn.Npc.Editor
{
    [CustomEditor(typeof(SceneLlmManager))]
    public class SceneLlmManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _provider;
        private SerializedProperty _sharedLocalModelSettings;
        private SerializedProperty _sharedRemoteModelSettings;
        private SerializedProperty _promptConfig;
        private SerializedProperty _islandContent;
        private SerializedProperty _triggerChannel;
        private SerializedProperty _llmEnabled;
        private SerializedProperty _saveSlotId;
        private SerializedProperty _playerProfile;
        private SerializedProperty _playerTransform;
        private SerializedProperty _proximityResolveRadius;
        private SerializedProperty _embeddingSettings;
        private SerializedProperty _recalledKnowledgeChannel;

        private SerializedObject _promptSo;
        private SerializedObject _embeddingSo;

        private void OnEnable()
        {
            _provider                  = serializedObject.FindProperty("provider");
            _sharedLocalModelSettings  = serializedObject.FindProperty("sharedLocalModelSettings");
            _sharedRemoteModelSettings = serializedObject.FindProperty("sharedRemoteModelSettings");
            _promptConfig              = serializedObject.FindProperty("promptConfig");
            _islandContent             = serializedObject.FindProperty("islandContent");
            _triggerChannel            = serializedObject.FindProperty("triggerChannel");
            _llmEnabled                = serializedObject.FindProperty("llmEnabled");
            _saveSlotId                = serializedObject.FindProperty("saveSlotId");
            _playerProfile             = serializedObject.FindProperty("playerProfile");
            _playerTransform           = serializedObject.FindProperty("playerTransform");
            _proximityResolveRadius    = serializedObject.FindProperty("proximityResolveRadius");
            _embeddingSettings         = serializedObject.FindProperty("embeddingSettings");
            _recalledKnowledgeChannel  = serializedObject.FindProperty("recalledKnowledgeChannel");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Scene-level LLM hub. Every NPC in this scene shares these settings — model, prompt config, island content, save slot, player.",
                MessageType.Info);

            Section("LLM");
            EditorGUILayout.PropertyField(_llmEnabled);
            EditorGUILayout.PropertyField(_provider);

            if (_provider.enumValueIndex == (int)LlmProvider.Local)
            {
                EditorGUILayout.PropertyField(_sharedLocalModelSettings);
                if (_sharedLocalModelSettings.objectReferenceValue == null)
                    EditorGUILayout.HelpBox("Assign LocalModelSettings so all NPCs can share a model config.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.PropertyField(_sharedRemoteModelSettings);
                if (_sharedRemoteModelSettings.objectReferenceValue == null)
                    EditorGUILayout.HelpBox("Assign RemoteModelSettings (Create → Dialogue → Remote Model Settings (OpenRouter)).", MessageType.Warning);

                if (GUILayout.Button("Open OpenRouter Settings…"))
                    OpenRouterSettingsWindow.ShowWindow();
            }

            Section("Prompt + Memory");
            EditorGUILayout.PropertyField(_promptConfig);
            if (_promptConfig.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an LlmPromptConfig asset. Without it the system prompt and JSON contract fall back to code defaults.",
                    MessageType.Warning);
            }
            else
            {
                var target = _promptConfig.objectReferenceValue;
                if (_promptSo == null || _promptSo.targetObject != target)
                    _promptSo = new SerializedObject(target);

                _promptSo.Update();
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_promptSo.FindProperty("systemPrompt"));
                EditorGUILayout.PropertyField(_promptSo.FindProperty("jsonOutputAddendum"));
                EditorGUILayout.PropertyField(_promptSo.FindProperty("recentTurnsLimit"));
                EditorGUILayout.PropertyField(_promptSo.FindProperty("memoryFactsLimit"));
                EditorGUILayout.PropertyField(_promptSo.FindProperty("maxFactLength"));
                EditorGUI.indentLevel--;
                _promptSo.ApplyModifiedProperties();
            }

            Section("Island Content");
            EditorGUILayout.PropertyField(_islandContent);
            if (_islandContent.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Drop an IslandContentHub component (with its island JSON assigned) into this field. NPCs, things, community knowledge, and signals all come from there.", MessageType.Warning);

            EditorGUILayout.PropertyField(_triggerChannel);
            if (_triggerChannel.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Optional. Assign a DialogueTriggerChannel asset. Signal ids an NPC fires in its envelope are raised on this channel.", MessageType.None);

            Section("Player");
            EditorGUILayout.PropertyField(_playerProfile);
            EditorGUILayout.PropertyField(_playerTransform);
            EditorGUILayout.PropertyField(_proximityResolveRadius);

            Section("Save Context");
            EditorGUILayout.PropertyField(_saveSlotId);

            Section("Semantic Memory");
            EditorGUILayout.PropertyField(_embeddingSettings);
            if (_embeddingSettings.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Optional. Assign an EmbeddingSettings asset (Create → Dialogue → Embedding Settings) to enable semantic memory + knowledge recall via Ollama all-minilm. " +
                    "Without it, dialogue uses keyword-based memory recall instead of semantic search.",
                    MessageType.Info);
            }
            else
            {
                var target = _embeddingSettings.objectReferenceValue;
                if (_embeddingSo == null || _embeddingSo.targetObject != target)
                    _embeddingSo = new SerializedObject(target);

                _embeddingSo.Update();
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_embeddingSo.FindProperty("endpointUrl"));
                EditorGUILayout.PropertyField(_embeddingSo.FindProperty("modelName"));
                EditorGUILayout.PropertyField(_embeddingSo.FindProperty("dimensions"));
                EditorGUILayout.PropertyField(_embeddingSo.FindProperty("recallTopK"));
                EditorGUILayout.PropertyField(_embeddingSo.FindProperty("minSimilarity"));
                EditorGUILayout.PropertyField(_embeddingSo.FindProperty("dedupThreshold"));
                EditorGUI.indentLevel--;
                _embeddingSo.ApplyModifiedProperties();
            }

            EditorGUILayout.PropertyField(_recalledKnowledgeChannel);
            if (_recalledKnowledgeChannel.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Optional. Assign a RecalledKnowledgeChannel asset (Create → Flynn → NPC → Recalled Knowledge Channel) to surface per-turn recalled knowledge in the NPC Info HUD.", MessageType.None);

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

}
