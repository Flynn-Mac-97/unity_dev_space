using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NpcPersonalityProfile))]
public class NpcPersonalityProfileEditor : Editor
{
    private SerializedProperty _npcId;
    private SerializedProperty _displayName;
    private SerializedProperty _roleDescription;
    private SerializedProperty _inGameSprite;
    private SerializedProperty _portraitSprite;
    private SerializedProperty _speakingStyle;
    private SerializedProperty _personalityTraits;
    private SerializedProperty _doRules;
    private SerializedProperty _dontRules;
    private SerializedProperty _fallbackLines;

    private bool _showIdentity = true;
    private bool _showVoice = true;
    private bool _showGuidelines = true;
    private bool _showFallback = true;

    private void OnEnable()
    {
        _npcId = serializedObject.FindProperty("npcId");
        _displayName = serializedObject.FindProperty("displayName");
        _roleDescription = serializedObject.FindProperty("roleDescription");
        _inGameSprite = serializedObject.FindProperty("inGameSprite");
        _portraitSprite = serializedObject.FindProperty("portraitSprite");
        _speakingStyle = serializedObject.FindProperty("speakingStyle");
        _personalityTraits = serializedObject.FindProperty("personalityTraits");
        _doRules = serializedObject.FindProperty("doRules");
        _dontRules = serializedObject.FindProperty("dontRules");
        _fallbackLines = serializedObject.FindProperty("fallbackLines");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox("Craft NPC personality for writers: identity, voice, and guardrails in one pass.", MessageType.Info);

        EditorGUILayout.BeginVertical("box");
        _showIdentity = EditorGUILayout.Foldout(_showIdentity, "Identity", true);
        if (_showIdentity)
        {
            EditorGUILayout.PropertyField(_npcId);
            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_roleDescription);
            EditorGUILayout.PropertyField(_inGameSprite);
            EditorGUILayout.PropertyField(_portraitSprite);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box");
        _showVoice = EditorGUILayout.Foldout(_showVoice, "Voice and Tone", true);
        if (_showVoice)
        {
            EditorGUILayout.PropertyField(_speakingStyle);
            EditorGUILayout.PropertyField(_personalityTraits);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box");
        _showGuidelines = EditorGUILayout.Foldout(_showGuidelines, "Guidelines", true);
        if (_showGuidelines)
        {
            EditorGUILayout.PropertyField(_doRules);
            EditorGUILayout.PropertyField(_dontRules);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box");
        _showFallback = EditorGUILayout.Foldout(_showFallback, "Fallback Lines", true);
        if (_showFallback)
        {
            EditorGUILayout.PropertyField(_fallbackLines, true);
        }
        EditorGUILayout.EndVertical();

        DrawValidation();

        EditorGUILayout.Space();
        if (GUILayout.Button("Open NPC Crafting Studio"))
        {
            NpcAuthoringStudioWindow.OpenWithProfile((NpcPersonalityProfile)target);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawValidation()
    {
        if (string.IsNullOrWhiteSpace(_npcId.stringValue))
        {
            EditorGUILayout.HelpBox("npcId is empty. Add a stable unique ID for save-memory lookup.", MessageType.Warning);
        }

        if (string.IsNullOrWhiteSpace(_displayName.stringValue))
        {
            EditorGUILayout.HelpBox("displayName is empty. Add the NPC's player-facing name.", MessageType.Warning);
        }

        if (_fallbackLines.arraySize == 0)
        {
            EditorGUILayout.HelpBox("Add at least one fallback line for offline/error cases.", MessageType.Warning);
        }
    }
}
