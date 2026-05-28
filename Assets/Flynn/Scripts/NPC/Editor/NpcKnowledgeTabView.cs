using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class NpcKnowledgeTabView
{
    private Vector2 _scroll;
    private SerializedObject _knowledgeSo;

    private ReorderableList _facts;
    private ReorderableList _beliefs;
    private ReorderableList _rumors;
    private ReorderableList _secrets;
    private ReorderableList _avoided;

    public void Draw(NpcDialogueAgentConfig config)
    {
        if (config == null) return;

        if (config.knowledge == null)
        {
            EditorGUILayout.HelpBox("No knowledge base assigned. Click Save to auto-create one.", MessageType.Warning);
            return;
        }

        if (_knowledgeSo == null || _knowledgeSo.targetObject != config.knowledge)
        {
            _knowledgeSo = new SerializedObject(config.knowledge);
            BuildEntryList(ref _facts, "knownFacts", "Known facts — things this NPC believes are true");
            BuildEntryList(ref _beliefs, "beliefs", "Beliefs — may be wrong, the world can contradict");
            BuildEntryList(ref _rumors, "rumors", "Rumors — hearsay, useful for misdirection");
            BuildEntryList(ref _secrets, "secrets", "Secrets — gated by trust or flags");
            BuildAvoidedList();
        }

        _knowledgeSo.Update();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Each entry: pick a Topic, write what the NPC knows about it, then choose whether to gate the reveal.",
            MessageType.None);

        _facts.DoLayoutList();
        _beliefs.DoLayoutList();
        _rumors.DoLayoutList();
        _secrets.DoLayoutList();

        EditorGUILayout.Space(6f);
        _avoided.DoLayoutList();

        _knowledgeSo.ApplyModifiedProperties();

        EditorGUILayout.EndScrollView();
    }

    private void BuildEntryList(ref ReorderableList list, string propertyName, string header)
    {
        var prop = _knowledgeSo.FindProperty(propertyName);
        var local = new ReorderableList(_knowledgeSo, prop, true, true, true, true);

        local.drawHeaderCallback = rect => EditorGUI.LabelField(rect, header.ToUpperInvariant(), EditorStyles.boldLabel);

        local.elementHeightCallback = index =>
        {
            var element = prop.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true) + 4f;
        };

        local.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = prop.GetArrayElementAtIndex(index);
            rect.y += 2f;
            rect.height -= 2f;
            EditorGUI.PropertyField(rect, element, new GUIContent("Entry " + index), true);
        };

        list = local;
    }

    private void BuildAvoidedList()
    {
        var prop = _knowledgeSo.FindProperty("avoidedTopics");
        _avoided = new ReorderableList(_knowledgeSo, prop, true, true, true, true);
        _avoided.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "AVOIDED TOPICS", EditorStyles.boldLabel);
        _avoided.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
        _avoided.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = prop.GetArrayElementAtIndex(index);
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, element, GUIContent.none);
        };
    }
}
