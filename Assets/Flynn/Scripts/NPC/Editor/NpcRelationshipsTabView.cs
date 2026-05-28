using UnityEditor;
using UnityEngine;

public class NpcRelationshipsTabView
{
    private Vector2 _scroll;
    private SerializedObject _so;

    public void Draw(NpcDialogueAgentConfig config)
    {
        if (config == null) return;

        if (config.relationship == null)
        {
            EditorGUILayout.HelpBox("No relationship defaults assigned. Click Save to auto-create.", MessageType.Warning);
            return;
        }

        if (_so == null || _so.targetObject != config.relationship)
            _so = new SerializedObject(config.relationship);

        _so.Update();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Three scalars — Trust, Affection, Suspicion (0-100). " +
            "Trust gates clue/secret reveals against the thresholds below. " +
            "Affection is for trade pricing and warmth. Suspicion makes the NPC defensive.",
            MessageType.None);

        Section("Starting values");
        EditorGUILayout.PropertyField(_so.FindProperty("startingTrust"));
        EditorGUILayout.PropertyField(_so.FindProperty("startingAffection"));
        EditorGUILayout.PropertyField(_so.FindProperty("startingSuspicion"));

        Section("Initial perception");
        EditorGUILayout.PropertyField(_so.FindProperty("initialPlayerStatus"));

        Section("Reveal thresholds");
        EditorGUILayout.PropertyField(_so.FindProperty("trustToShareClues"));
        EditorGUILayout.PropertyField(_so.FindProperty("trustToShareSecrets"));

        _so.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();
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
