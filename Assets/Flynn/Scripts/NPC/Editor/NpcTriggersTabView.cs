using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class NpcTriggersTabView
{
    private Vector2 _scroll;
    private SerializedObject _triggersSo;
    private ReorderableList _list;

    public void Draw(NpcDialogueAgentConfig config)
    {
        if (config == null) return;

        if (config.triggers == null)
        {
            EditorGUILayout.HelpBox("No trigger set assigned. Click Save to auto-create one.", MessageType.Warning);
            return;
        }

        if (_triggersSo == null || _triggersSo.targetObject != config.triggers)
        {
            _triggersSo = new SerializedObject(config.triggers);
            var arr = _triggersSo.FindProperty("triggers");
            _list = new ReorderableList(_triggersSo, arr, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "DIALOGUE TRIGGERS", EditorStyles.boldLabel),
                elementHeightCallback = i => EditorGUI.GetPropertyHeight(arr.GetArrayElementAtIndex(i), true) + 4f,
                drawElementCallback = (rect, i, active, focused) =>
                {
                    var element = arr.GetArrayElementAtIndex(i);
                    rect.y += 2f;
                    EditorGUI.PropertyField(rect, element, new GUIContent("Trigger " + i), true);
                },
            };
        }

        _triggersSo.Update();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.HelpBox(
            "Triggers are named flags the dialogue runtime can fire (story beat reached, clue unlocked, etc.). " +
            "Choose a Topic and a kind so the prompt knows whether this is a clue, secret, or forbidden subject.",
            MessageType.None);
        _list.DoLayoutList();
        _triggersSo.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();
    }
}
