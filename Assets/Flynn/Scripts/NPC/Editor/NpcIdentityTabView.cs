using UnityEditor;
using UnityEngine;

public class NpcIdentityTabView
{
    private Vector2 _scroll;
    private SerializedObject _profileSo;
    private SerializedObject _configSo;

    public void Draw(NpcDialogueAgentConfig config)
    {
        if (config == null) return;

        if (config.personalityProfile == null)
        {
            EditorGUILayout.HelpBox("No personality profile assigned. Click Save to auto-create one.", MessageType.Warning);
            return;
        }

        EnsureSo(ref _profileSo, config.personalityProfile);
        EnsureSo(ref _configSo, config);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        _profileSo.Update();
        _configSo.Update();

        Section("Identity");
        EditorGUILayout.PropertyField(_profileSo.FindProperty("npcId"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("displayName"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("roleDescription"));

        Section("Gameplay role");
        var rolesProp = _configSo.FindProperty("roles");
        rolesProp.intValue = (int)(NpcGameplayRoles)EditorGUILayout.EnumFlagsField(
            new GUIContent("Roles", "Which gameplay functions this NPC fulfills."),
            (NpcGameplayRoles)rolesProp.intValue);

        Section("Visual assets");
        EditorGUILayout.PropertyField(_profileSo.FindProperty("inGameSprite"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("portraitSprite"));

        Section("Voice and personality");
        EditorGUILayout.PropertyField(_profileSo.FindProperty("speakingStyle"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("personalityTraits"));

        Section("Guidelines");
        EditorGUILayout.PropertyField(_profileSo.FindProperty("doRules"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("dontRules"));

        Section("Fallback lines");
        EditorGUILayout.PropertyField(_profileSo.FindProperty("fallbackLines"), true);

        _profileSo.ApplyModifiedProperties();
        _configSo.ApplyModifiedProperties();

        EditorGUILayout.EndScrollView();
    }

    private static void EnsureSo<T>(ref SerializedObject so, T target) where T : Object
    {
        if (target == null) { so = null; return; }
        if (so == null || so.targetObject != target) so = new SerializedObject(target);
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
