using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class NpcIdentityTabView
{
    private Vector2 _scroll;
    private SerializedObject _profileSo;
    private SerializedObject _configSo;
    private string _newCapabilityDraft = string.Empty;

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

        EditorGUILayout.HelpBox(
            "Authoring data for this NPC. The prompt template that consumes these fields lives on the Dashboard tab.",
            MessageType.Info);

        Section("Identity");
        EditorGUILayout.PropertyField(_profileSo.FindProperty("npcId"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("displayName"));

        Section("Gameplay role");
        var rolesProp = _configSo.FindProperty("roles");
        rolesProp.intValue = (int)(NpcGameplayRoles)EditorGUILayout.EnumFlagsField(
            new GUIContent("Roles", "Which gameplay functions this NPC fulfills."),
            (NpcGameplayRoles)rolesProp.intValue);

        Section("Visual assets");
        EditorGUILayout.PropertyField(_profileSo.FindProperty("inGameSprite"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("portraitSprite"));

        Section("Persona details");
        EditorGUILayout.PropertyField(_profileSo.FindProperty("roleDescription"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("speakingStyle"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("personalityTraits"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("doRules"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("dontRules"));

        Section("Things you can do");
        EditorGUILayout.HelpBox(
            "Concrete in-game actions this NPC can actually perform. The LLM sees these via the {capabilities} token, so it will not offer to do things outside this list.",
            MessageType.None);
        var capsProp = _profileSo.FindProperty("capabilities");
        DrawCapabilityPicker(config, capsProp);
        DrawCapabilitySuggestions(config, capsProp);

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

    private void DrawCapabilityPicker(NpcDialogueAgentConfig config, SerializedProperty capsProp)
    {
        var library = ResolveLibrary();

        if (library == null)
        {
            EditorGUILayout.HelpBox(
                "No NpcCapabilityLibrary is assigned on the SceneLlmManager. Without a library, capabilities cannot be picked from a list — assign one to enable the dropdown.",
                MessageType.Warning);
            EditorGUILayout.PropertyField(capsProp, new GUIContent("Capabilities (manual)"), true);
            return;
        }

        // Build the dropdown options from the library.
        var libEntries = library.entries;
        int libCount = libEntries != null ? libEntries.Count : 0;
        string[] options = new string[libCount];
        for (int i = 0; i < libCount; i++)
            options[i] = string.IsNullOrWhiteSpace(libEntries[i]) ? "(empty)" : libEntries[i];

        // Read the current selection mask from the profile's list.
        var selected = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < capsProp.arraySize; i++)
        {
            string v = capsProp.GetArrayElementAtIndex(i).stringValue;
            if (!string.IsNullOrWhiteSpace(v)) selected.Add(v.Trim());
        }

        int mask = 0;
        for (int i = 0; i < libCount && i < 32; i++)
            if (!string.IsNullOrWhiteSpace(libEntries[i]) && selected.Contains(libEntries[i].Trim()))
                mask |= 1 << i;

        EditorGUI.BeginChangeCheck();
        int newMask = libCount == 0
            ? 0
            : EditorGUILayout.MaskField(new GUIContent("Capabilities"), mask, options);
        if (EditorGUI.EndChangeCheck() && libCount > 0)
        {
            ApplyMaskToProfileList(capsProp, libEntries, newMask, preserveCustom: true);
        }

        // Preserve any "custom" capabilities the profile has that aren't in the library.
        DrawCustomLeftovers(capsProp, library);

        // Inline add to library.
        EditorGUILayout.BeginHorizontal();
        _newCapabilityDraft = EditorGUILayout.TextField(_newCapabilityDraft, GUILayout.MinWidth(60f));
        GUI.enabled = !string.IsNullOrWhiteSpace(_newCapabilityDraft);
        if (GUILayout.Button("Add to library", GUILayout.Width(120f)))
        {
            string entry = _newCapabilityDraft.Trim();
            if (library.TryAdd(entry))
            {
                UnityEditor.EditorUtility.SetDirty(library);
                AppendToProfile(capsProp, entry);
            }
            _newCapabilityDraft = string.Empty;
            GUI.FocusControl(null);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (libCount == 0)
            EditorGUILayout.HelpBox("The library is empty. Type a capability above and click \"Add to library\", or use the example chips below.", MessageType.Info);
    }

    private static void ApplyMaskToProfileList(SerializedProperty capsProp, List<string> libEntries, int mask, bool preserveCustom)
    {
        // Build the new list: every entry in the library whose bit is set, plus
        // every "custom" entry currently on the profile that isn't in the library.
        var fromLibrary = new List<string>();
        var libraryKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < libEntries.Count && i < 32; i++)
        {
            if (string.IsNullOrWhiteSpace(libEntries[i])) continue;
            libraryKeys.Add(libEntries[i].Trim());
            if ((mask & (1 << i)) != 0)
                fromLibrary.Add(libEntries[i].Trim());
        }

        var customs = new List<string>();
        if (preserveCustom)
        {
            for (int i = 0; i < capsProp.arraySize; i++)
            {
                string v = capsProp.GetArrayElementAtIndex(i).stringValue;
                if (string.IsNullOrWhiteSpace(v)) continue;
                string trimmed = v.Trim();
                if (!libraryKeys.Contains(trimmed)) customs.Add(trimmed);
            }
        }

        capsProp.ClearArray();
        for (int i = 0; i < fromLibrary.Count; i++)
        {
            capsProp.InsertArrayElementAtIndex(i);
            capsProp.GetArrayElementAtIndex(i).stringValue = fromLibrary[i];
        }
        int baseIdx = capsProp.arraySize;
        for (int i = 0; i < customs.Count; i++)
        {
            capsProp.InsertArrayElementAtIndex(baseIdx + i);
            capsProp.GetArrayElementAtIndex(baseIdx + i).stringValue = customs[i];
        }
    }

    private static void DrawCustomLeftovers(SerializedProperty capsProp, NpcCapabilityLibrary library)
    {
        var customs = new System.Collections.Generic.List<int>();
        for (int i = 0; i < capsProp.arraySize; i++)
        {
            string v = capsProp.GetArrayElementAtIndex(i).stringValue;
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (library.Contains(v)) continue;
            customs.Add(i);
        }

        if (customs.Count == 0) return;

        EditorGUILayout.LabelField("Custom (not in library)", EditorStyles.miniBoldLabel);
        for (int i = customs.Count - 1; i >= 0; i--)
        {
            int idx = customs[i];
            string text = capsProp.GetArrayElementAtIndex(idx).stringValue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("• " + text);
            if (GUILayout.Button("Add to library", GUILayout.Width(120f)))
            {
                if (library.TryAdd(text))
                    UnityEditor.EditorUtility.SetDirty(library);
            }
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                capsProp.DeleteArrayElementAtIndex(idx);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private static void AppendToProfile(SerializedProperty capsProp, string entry)
    {
        for (int i = 0; i < capsProp.arraySize; i++)
        {
            if (string.Equals(capsProp.GetArrayElementAtIndex(i).stringValue, entry, System.StringComparison.OrdinalIgnoreCase))
                return;
        }
        int idx = capsProp.arraySize;
        capsProp.InsertArrayElementAtIndex(idx);
        capsProp.GetArrayElementAtIndex(idx).stringValue = entry;
    }

    private static NpcCapabilityLibrary ResolveLibrary()
    {
        var manager = SceneLlmManager.Instance != null
            ? SceneLlmManager.Instance
            : Object.FindObjectOfType<SceneLlmManager>();
        return manager != null ? manager.capabilityLibrary : null;
    }

    private static void DrawCapabilitySuggestions(NpcDialogueAgentConfig config, SerializedProperty capsProp)
    {
        var existing = new List<string>(capsProp.arraySize);
        for (int i = 0; i < capsProp.arraySize; i++)
            existing.Add(capsProp.GetArrayElementAtIndex(i).stringValue);

        var suggestions = NpcCapabilityExamples.SuggestionsFor(config.roles, existing);
        if (suggestions.Count == 0) return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Examples for current roles (click to add to library + select)", EditorStyles.miniBoldLabel);

        var library = ResolveLibrary();

        float available = EditorGUIUtility.currentViewWidth - 40f;
        float used = 0f;
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < suggestions.Count; i++)
        {
            string s = suggestions[i];
            var content = new GUIContent("+ " + s);
            float width = EditorStyles.miniButton.CalcSize(content).x + 4f;
            if (used + width > available)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                used = 0f;
            }

            if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(width)))
            {
                if (library != null && library.TryAdd(s))
                    UnityEditor.EditorUtility.SetDirty(library);
                AppendToProfile(capsProp, s);
            }
            used += width;
        }
        EditorGUILayout.EndHorizontal();
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
