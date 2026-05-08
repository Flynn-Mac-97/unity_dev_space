using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class NpcAuthoringStudioWindow : EditorWindow
{
    private enum StudioTab
    {
        Dashboard,
        Npc,
        Ai
    }

    private NpcDialogueAgentConfig _config;
    private SceneLlmManager _sceneLlmManager;
    private StudioTab _tab;

    private SerializedObject _configSo;
    private SerializedObject _profileSo;
    private SerializedObject _templateSo;
    private SerializedObject _memorySo;
    private SerializedObject _sceneLlmManagerSo;

    private Vector2 _scroll;
    private NpcDialogueAgentConfig[] _npcConfigCache = Array.Empty<NpcDialogueAgentConfig>();
    private string[] _npcConfigLabels = Array.Empty<string>();
    private int _selectedConfigIndex = -1;

    private string _sampleMemorySummary =
        "- Player helped repair a wind pump yesterday.\n" +
        "- NPC now trusts player with route advice.";

    [MenuItem("Tools/Dialogue/NPC Crafting Studio")]
    public static void Open()
    {
        var window = GetWindow<NpcAuthoringStudioWindow>("NPC Crafting Studio");
        window.minSize = new Vector2(860f, 560f);
        window.Show();
    }

    public static void OpenWithConfig(NpcDialogueAgentConfig config)
    {
        var window = GetWindow<NpcAuthoringStudioWindow>("NPC Crafting Studio");
        window.minSize = new Vector2(860f, 560f);
        window.SetConfig(config);
        window.Show();
    }

    public static void OpenWithTemplate(NpcPromptTemplate template)
    {
        OpenWithConfig(FindConfigByTemplate(template));
    }

    public static void OpenWithProfile(NpcPersonalityProfile profile)
    {
        OpenWithConfig(FindConfigByProfile(profile));
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("NPC Crafting Studio", EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow").image);
        RefreshNpcConfigCache();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (_config == null)
        {
            DrawEmptyState();
            return;
        }

        EnsureSerializedObjects();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawStatusCard();
        DrawNpcLibraryPanel();

        EditorGUILayout.Space(4f);
        _tab = (StudioTab)GUILayout.Toolbar((int)_tab, new[] { "Dashboard", "NPC", "AI + Memory" });

        EditorGUILayout.Space(8f);
        switch (_tab)
        {
            case StudioTab.Dashboard:
                DrawDashboardTab();
                break;
            case StudioTab.Npc:
                DrawPersonalityTab();
                break;
            case StudioTab.Ai:
                DrawAiTab();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void SetConfig(NpcDialogueAgentConfig config)
    {
        _config = config;
        _configSo = null;
        _profileSo = null;
        _templateSo = null;
        _memorySo = null;
        _sceneLlmManagerSo = null;
        SyncSelectedConfigIndex();
        Repaint();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("NPC", EditorStyles.miniLabel, GUILayout.Width(28f));
        _config = (NpcDialogueAgentConfig)EditorGUILayout.ObjectField(_config, typeof(NpcDialogueAgentConfig), false, GUILayout.MinWidth(220f));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Switch NPC", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            ShowNpcSwitchMenu();

        if (GUILayout.Button("Refresh NPCs", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            RefreshNpcConfigCache();

        if (GUILayout.Button("New NPC", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            ShowCreateNpcNamePopup();

        if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(95f)))
        {
            var selected = Selection.activeObject as NpcDialogueAgentConfig;
            if (selected != null) SetConfig(selected);
        }

        if (GUILayout.Button("Create Missing Assets", EditorStyles.toolbarButton, GUILayout.Width(130f)))
            CreateMissingAssets();

        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            SaveAndApplyNpcVisuals();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEmptyState()
    {
        EditorGUILayout.Space(12f);
        EditorGUILayout.HelpBox("Assign an NPC Dialogue Agent Config to start authoring.", MessageType.Info);

        var maybeConfig = Selection.activeObject as NpcDialogueAgentConfig;
        if (maybeConfig != null && GUILayout.Button("Use Selected Config"))
            SetConfig(maybeConfig);
    }

    private void DrawStatusCard()
    {
        bool ready = _config != null && _config.HasRequiredReferences();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Authoring Status", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            ready
                ? "Ready: Personality, prompt, and memory assets are assigned."
                : "Missing references: assign all required NPC assets to complete this setup.",
            ready ? MessageType.Info : MessageType.Warning);

        if (_sceneLlmManager == null)
            EditorGUILayout.HelpBox("No SceneLlmManager found in open scenes. Add one so all NPCs share the same model.", MessageType.Warning);

        EditorGUILayout.EndVertical();
    }

    private void DrawNpcLibraryPanel()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("NPC Library", EditorStyles.boldLabel);

        if (_npcConfigCache == null || _npcConfigCache.Length == 0)
            RefreshNpcConfigCache();

        if (_npcConfigCache == null || _npcConfigCache.Length == 0)
        {
            EditorGUILayout.HelpBox("No NPC configs found yet. Create one to start authoring.", MessageType.Info);
            EditorGUILayout.HelpBox("Use the top toolbar New NPC button to create one.", MessageType.None);

            EditorGUILayout.EndVertical();
            return;
        }

        SyncSelectedConfigIndex();

        int nextIndex = EditorGUILayout.Popup("Active NPC", Mathf.Max(0, _selectedConfigIndex), _npcConfigLabels);
        if (nextIndex >= 0 && nextIndex < _npcConfigCache.Length && nextIndex != _selectedConfigIndex)
        {
            SetConfig(_npcConfigCache[nextIndex]);
        }

        EditorGUILayout.HelpBox("Switch, create, and refresh actions are available in the top toolbar.", MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DrawDashboardTab()
    {
        if (_config == null) return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("High-Level Preview", EditorStyles.boldLabel);

        string npcName = _config.personalityProfile != null
            ? _config.personalityProfile.GetSafeDisplayName()
            : "Unassigned NPC";
        string npcId = _config.personalityProfile != null
            ? _config.personalityProfile.GetSafeNpcId()
            : "npc.unknown";
        string role = _config.personalityProfile != null
            ? _config.personalityProfile.roleDescription
            : "Assign a personality profile to define this NPC.";

        EditorGUILayout.LabelField("Name", npcName);
        EditorGUILayout.LabelField("ID", npcId);
        EditorGUILayout.LabelField("Role", string.IsNullOrWhiteSpace(role) ? "(empty)" : role.Trim());
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Visual Assets", EditorStyles.boldLabel);
        Sprite inGame = _config.personalityProfile != null ? _config.personalityProfile.inGameSprite : null;
        Sprite portrait = _config.personalityProfile != null ? _config.personalityProfile.portraitSprite : null;
        EditorGUILayout.ObjectField("In-Game Sprite", inGame, typeof(Sprite), false);
        EditorGUILayout.ObjectField("Portrait Sprite", portrait, typeof(Sprite), false);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Linked Assets", EditorStyles.boldLabel);
        EditorGUILayout.ObjectField("Config", _config, typeof(NpcDialogueAgentConfig), false);
        EditorGUILayout.ObjectField("Personality", _config.personalityProfile, typeof(NpcPersonalityProfile), false);
        EditorGUILayout.ObjectField("Prompt", _config.promptTemplate, typeof(NpcPromptTemplate), false);
        EditorGUILayout.ObjectField("Memory", _config.memorySettings, typeof(NpcMemorySettings), false);
        EditorGUILayout.ObjectField("Scene Manager", _sceneLlmManager, typeof(SceneLlmManager), true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Missing Assets"))
            CreateMissingAssets();

        if (GUILayout.Button("Jump To NPC Tab"))
            _tab = StudioTab.Npc;

        if (GUILayout.Button("Jump To AI + Memory Tab"))
            _tab = StudioTab.Ai;
        EditorGUILayout.EndHorizontal();

        DrawPromptPreviewPanel();
    }

    private void DrawPersonalityTab()
    {
        if (_profileSo == null)
        {
            EditorGUILayout.HelpBox("No personality profile assigned.", MessageType.Warning);
            return;
        }

        _profileSo.Update();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_profileSo.FindProperty("npcId"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("displayName"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("roleDescription"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Visual Assets", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_profileSo.FindProperty("inGameSprite"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("portraitSprite"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Voice and Personality", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_profileSo.FindProperty("speakingStyle"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("personalityTraits"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("doRules"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("dontRules"));
        EditorGUILayout.PropertyField(_profileSo.FindProperty("fallbackLines"), true);
        EditorGUILayout.EndVertical();

        _profileSo.ApplyModifiedProperties();
    }

    private void DrawAiTab()
    {
        if (_templateSo == null)
            EditorGUILayout.HelpBox("No prompt template assigned.", MessageType.Warning);

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
        if (_templateSo != null)
        {
            _templateSo.Update();
            EditorGUILayout.PropertyField(_templateSo.FindProperty("coreSystemPrompt"), GUIContent.none);
            EditorGUILayout.PropertyField(_templateSo.FindProperty("memorySummaryTemplate"), GUIContent.none);
            EditorGUILayout.PropertyField(_templateSo.FindProperty("optionalWorldContext"), GUIContent.none);
            _templateSo.ApplyModifiedProperties();
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a prompt template in Config to edit prompt fields.", MessageType.Info);
        }

        DrawTokenPalette();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Runtime + Memory", EditorStyles.boldLabel);

        if (_configSo != null)
        {
            _configSo.Update();
            EditorGUILayout.PropertyField(_configSo.FindProperty("useLocalModel"));
            EditorGUILayout.PropertyField(_configSo.FindProperty("fallbackReply"));
            _configSo.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Memory Settings", EditorStyles.boldLabel);
        if (_memorySo != null)
        {
            _memorySo.Update();
            EditorGUILayout.PropertyField(_memorySo.FindProperty("recentTurnsLimit"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("memoryFactsLimit"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("maxFactLength"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("injectedRecentTurns"));
            EditorGUILayout.PropertyField(_memorySo.FindProperty("injectedFacts"));
            _memorySo.ApplyModifiedProperties();
        }
        else
        {
            EditorGUILayout.HelpBox("No memory settings assigned.", MessageType.Warning);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Scene LLM Manager", EditorStyles.boldLabel);
        _sceneLlmManager = (SceneLlmManager)EditorGUILayout.ObjectField("Manager", _sceneLlmManager, typeof(SceneLlmManager), true);
        EnsureSerializedObject(ref _sceneLlmManagerSo, _sceneLlmManager);
        if (_sceneLlmManagerSo != null)
        {
            _sceneLlmManagerSo.Update();
            EditorGUILayout.PropertyField(_sceneLlmManagerSo.FindProperty("llmEnabled"));
            EditorGUILayout.PropertyField(_sceneLlmManagerSo.FindProperty("sharedLocalModelSettings"));
            EditorGUILayout.PropertyField(_sceneLlmManagerSo.FindProperty("saveSlotId"));
            _sceneLlmManagerSo.ApplyModifiedProperties();
        }
        else
        {
            EditorGUILayout.HelpBox("No SceneLlmManager assigned or found.", MessageType.Warning);
            if (GUILayout.Button("Create Scene LLM Manager"))
                CreateSceneLlmManagerInActiveScene();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        DrawPromptPreviewPanel();
    }

    private void DrawPromptPreviewPanel()
    {
        if (_config.promptTemplate == null)
        {
            EditorGUILayout.HelpBox("No prompt template assigned.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Memory Summary Input", EditorStyles.boldLabel);
        _sampleMemorySummary = EditorGUILayout.TextArea(_sampleMemorySummary, GUILayout.MinHeight(90f));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6f);

        string assembled = _config.promptTemplate.BuildAssembledPrompt(_config.personalityProfile, _sampleMemorySummary);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Assembled System Prompt", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(assembled, EditorStyles.textArea, GUILayout.MinHeight(220f));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy Prompt"))
            EditorGUIUtility.systemCopyBuffer = assembled;

        if (GUILayout.Button("Open Floating Preview"))
            NpcPromptPreviewWindow.Open(_config.promptTemplate, _config.personalityProfile);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawTokenPalette()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Token Palette", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Click a token to copy it.", EditorStyles.miniLabel);

        DrawTokenRow(NpcPromptTemplate.TokenNpcName, NpcPromptTemplate.TokenRoleDescription, NpcPromptTemplate.TokenSpeakingStyle);
        DrawTokenRow(NpcPromptTemplate.TokenPersonalityTraits, NpcPromptTemplate.TokenDoRules, NpcPromptTemplate.TokenDontRules);
        DrawTokenRow(NpcPromptTemplate.TokenMemorySummary);

        EditorGUILayout.EndVertical();
    }

    private static void DrawTokenRow(params string[] tokens)
    {
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < tokens.Length; i++)
        {
            if (GUILayout.Button(tokens[i]))
                EditorGUIUtility.systemCopyBuffer = tokens[i];
        }
        EditorGUILayout.EndHorizontal();
    }

    private void EnsureSerializedObjects()
    {
        if (_sceneLlmManager == null)
            _sceneLlmManager = UnityEngine.Object.FindObjectOfType<SceneLlmManager>();

        EnsureSerializedObject(ref _configSo, _config);
        EnsureSerializedObject(ref _profileSo, _config.personalityProfile);
        EnsureSerializedObject(ref _templateSo, _config.promptTemplate);
        EnsureSerializedObject(ref _memorySo, _config.memorySettings);
        EnsureSerializedObject(ref _sceneLlmManagerSo, _sceneLlmManager);
    }

    private static void EnsureSerializedObject<T>(ref SerializedObject so, T target) where T : UnityEngine.Object
    {
        if (target == null)
        {
            so = null;
            return;
        }

        if (so == null || so.targetObject != target)
            so = new SerializedObject(target);
    }

    private void CreateMissingAssets(string explicitPrefix = null)
    {
        if (_config == null)
        {
            EditorUtility.DisplayDialog("NPC Crafting Studio", "Assign a config first.", "OK");
            return;
        }

        string configPath = AssetDatabase.GetAssetPath(_config);
        if (string.IsNullOrEmpty(configPath))
        {
            EditorUtility.DisplayDialog("NPC Crafting Studio", "Config must be a saved asset in the project.", "OK");
            return;
        }

        string folder = Path.GetDirectoryName(configPath);
        if (string.IsNullOrEmpty(folder)) folder = "Assets";
        folder = folder.Replace('\\', '/');

        string baseName = !string.IsNullOrWhiteSpace(explicitPrefix)
            ? SanitizePrefix(explicitPrefix)
            : GetAssetPrefixFromConfigName(_config.name);

        if (_config.personalityProfile == null)
            _config.personalityProfile = CreateAsset<NpcPersonalityProfile>(folder, baseName + "_Personality");

        if (_config.promptTemplate == null)
            _config.promptTemplate = CreateAsset<NpcPromptTemplate>(folder, baseName + "_PromptTemplate");

        if (_config.memorySettings == null)
            _config.memorySettings = CreateAsset<NpcMemorySettings>(folder, baseName + "_MemorySettings");

        EditorUtility.SetDirty(_config);
        SaveAll();
        EnsureSerializedObjects();
        RefreshNpcConfigCache();
    }

    private void RefreshNpcConfigCache()
    {
        string[] guids = AssetDatabase.FindAssets("t:NpcDialogueAgentConfig");
        var list = new List<NpcDialogueAgentConfig>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            NpcDialogueAgentConfig config = AssetDatabase.LoadAssetAtPath<NpcDialogueAgentConfig>(path);
            if (config != null)
                list.Add(config);
        }

        list.Sort((a, b) => string.Compare(GetConfigLabel(a), GetConfigLabel(b), StringComparison.OrdinalIgnoreCase));

        _npcConfigCache = list.ToArray();
        _npcConfigLabels = new string[_npcConfigCache.Length];
        for (int i = 0; i < _npcConfigCache.Length; i++)
            _npcConfigLabels[i] = GetConfigLabel(_npcConfigCache[i]);

        SyncSelectedConfigIndex();
    }

    private void ShowNpcSwitchMenu()
    {
        RefreshNpcConfigCache();

        if (_npcConfigCache == null || _npcConfigCache.Length == 0)
        {
            EditorUtility.DisplayDialog("NPC Crafting Studio", "No NPC Dialogue Agent Config assets found.", "OK");
            return;
        }

        var menu = new GenericMenu();
        for (int i = 0; i < _npcConfigCache.Length; i++)
        {
            NpcDialogueAgentConfig entry = _npcConfigCache[i];
            bool isCurrent = entry == _config;
            menu.AddItem(new GUIContent(_npcConfigLabels[i]), isCurrent, () => SetConfig(entry));
        }

        menu.ShowAsContext();
    }

    private void CreateNpcConfigWithDefaults(string requestedName)
    {
        EnsureFolder("Assets", "ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects", "Dialogue");

        string prefix = SanitizePrefix(requestedName);
        string displayName = string.IsNullOrWhiteSpace(requestedName) ? prefix : requestedName.Trim();

        var newConfig = ScriptableObject.CreateInstance<NpcDialogueAgentConfig>();
        string path = AssetDatabase.GenerateUniqueAssetPath(
            "Assets/ScriptableObjects/Dialogue/" + prefix + "_NpcDialogueAgentConfig.asset");
        AssetDatabase.CreateAsset(newConfig, path);

        SetConfig(newConfig);
        CreateMissingAssets(prefix);

        if (newConfig.personalityProfile != null)
        {
            newConfig.personalityProfile.displayName = displayName;
            if (string.IsNullOrWhiteSpace(newConfig.personalityProfile.npcId)
                || newConfig.personalityProfile.npcId == "npc.stranger")
            {
                newConfig.personalityProfile.npcId = "npc." + prefix.ToLowerInvariant();
            }

            EditorUtility.SetDirty(newConfig.personalityProfile);
            SaveAll();
        }

        Selection.activeObject = newConfig;
        RefreshNpcConfigCache();
    }

    private void ShowCreateNpcNamePopup()
    {
        NpcNamePromptWindow.Open("Create New NPC", "NewNpc", CreateNpcConfigWithDefaults);
    }

    private static void EnsureFolder(string parentPath, string folderName)
    {
        string fullPath = parentPath + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parentPath, folderName);
    }

    private static string GetConfigLabel(NpcDialogueAgentConfig config)
    {
        if (config == null)
            return "Missing Config";

        string displayName = config.personalityProfile != null
            ? config.personalityProfile.GetSafeDisplayName()
            : "Unassigned NPC";

        return string.Format("{0} ({1})", displayName, config.name);
    }

    private static string SanitizePrefix(string rawPrefix)
    {
        string prefix = string.IsNullOrWhiteSpace(rawPrefix) ? "NPC" : rawPrefix.Trim();

        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
            prefix = prefix.Replace(invalid[i], '_');

        prefix = prefix.Replace(' ', '_');

        while (prefix.Contains("__"))
            prefix = prefix.Replace("__", "_");

        prefix = prefix.Trim('_');
        return string.IsNullOrWhiteSpace(prefix) ? "NPC" : prefix;
    }

    private static string GetAssetPrefixFromConfigName(string configName)
    {
        string baseName = string.IsNullOrWhiteSpace(configName) ? "NPC" : configName.Trim();

        const string suffixWithUnderscore = "_NpcDialogueAgentConfig";
        const string suffix = "NpcDialogueAgentConfig";

        if (baseName.EndsWith(suffixWithUnderscore, StringComparison.OrdinalIgnoreCase))
            baseName = baseName.Substring(0, baseName.Length - suffixWithUnderscore.Length);
        else if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            baseName = baseName.Substring(0, baseName.Length - suffix.Length);

        return SanitizePrefix(baseName);
    }

    private void SaveAndApplyNpcVisuals()
    {
        SaveAll();

        if (_config == null || _config.personalityProfile == null)
        {
            ShowNotification(new GUIContent("Saved."));
            return;
        }

        Sprite inGameSprite = _config.personalityProfile.inGameSprite;
        Sprite portraitSprite = _config.personalityProfile.portraitSprite;

        NpcDialogueAuthoringLink[] links = UnityEngine.Object.FindObjectsOfType<NpcDialogueAuthoringLink>(true);
        int updatedNpcSprites = 0;
        int updatedPortraitSprites = 0;

        for (int i = 0; i < links.Length; i++)
        {
            NpcDialogueAuthoringLink link = links[i];
            if (link == null || link.agentConfig != _config)
                continue;

            SpriteRenderer inGameTarget = ResolveInGameTarget(link);
            if (inGameTarget != null && inGameTarget.sprite != inGameSprite)
            {
                inGameTarget.sprite = inGameSprite;
                EditorUtility.SetDirty(inGameTarget);
                EditorSceneManager.MarkSceneDirty(inGameTarget.gameObject.scene);
                updatedNpcSprites++;
            }

            SpriteRenderer portraitTarget = ResolvePortraitTarget(link);
            if (portraitTarget != null && portraitTarget.sprite != portraitSprite)
            {
                portraitTarget.sprite = portraitSprite;
                EditorUtility.SetDirty(portraitTarget);
                EditorSceneManager.MarkSceneDirty(portraitTarget.gameObject.scene);
                updatedPortraitSprites++;
            }
        }

        SaveAll();
        ShowNotification(new GUIContent(string.Format(
            "Saved. Updated sprites: in-game {0}, portrait {1}",
            updatedNpcSprites,
            updatedPortraitSprites)));
    }

    private static SpriteRenderer ResolveInGameTarget(NpcDialogueAuthoringLink link)
    {
        if (link == null)
            return null;

        if (link.inGameSpriteRenderer != null)
            return link.inGameSpriteRenderer;

        SpriteRenderer onRoot = link.GetComponent<SpriteRenderer>();
        if (onRoot != null)
            return onRoot;

        return link.GetComponentInChildren<SpriteRenderer>(true);
    }

    private static SpriteRenderer ResolvePortraitTarget(NpcDialogueAuthoringLink link)
    {
        if (link == null)
            return null;

        if (link.portraitSpriteRenderer != null)
            return link.portraitSpriteRenderer;

        SpriteRenderer[] renderers = link.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer current = renderers[i];
            if (current == null) continue;

            string objectName = current.gameObject.name;
            if (!string.IsNullOrWhiteSpace(objectName)
                && objectName.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return current;
            }
        }

        return null;
    }

    private void SyncSelectedConfigIndex()
    {
        _selectedConfigIndex = -1;

        if (_config == null || _npcConfigCache == null)
            return;

        for (int i = 0; i < _npcConfigCache.Length; i++)
        {
            if (_npcConfigCache[i] == _config)
            {
                _selectedConfigIndex = i;
                break;
            }
        }
    }


internal class NpcNamePromptWindow : EditorWindow
{
    private Action<string> _onSubmit;
    private string _npcName;

    public static void Open(string title, string defaultName, Action<string> onSubmit)
    {
        var window = CreateInstance<NpcNamePromptWindow>();
        window.titleContent = new GUIContent(title);
        window.minSize = new Vector2(360f, 96f);
        window.maxSize = new Vector2(640f, 96f);
        window._npcName = string.IsNullOrWhiteSpace(defaultName) ? "NewNpc" : defaultName;
        window._onSubmit = onSubmit;
        window.ShowUtility();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("NPC Name", EditorStyles.boldLabel);
        _npcName = EditorGUILayout.TextField(_npcName);

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Cancel"))
        {
            Close();
            return;
        }

        if (GUILayout.Button("Create"))
        {
            Submit();
            return;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(_npcName))
        {
            EditorUtility.DisplayDialog("Create New NPC", "Please enter an NPC name.", "OK");
            return;
        }

        _onSubmit?.Invoke(_npcName.Trim());
        Close();
    }
}
    private void CreateSceneLlmManagerInActiveScene()
    {
        if (_sceneLlmManager != null)
        {
            Selection.activeObject = _sceneLlmManager.gameObject;
            return;
        }

        var existing = UnityEngine.Object.FindObjectOfType<SceneLlmManager>();
        if (existing != null)
        {
            _sceneLlmManager = existing;
            Selection.activeObject = existing.gameObject;
            EnsureSerializedObjects();
            return;
        }

        var go = new GameObject("Scene_LLM_Manager");
        _sceneLlmManager = go.AddComponent<SceneLlmManager>();
        Undo.RegisterCreatedObjectUndo(go, "Create Scene LLM Manager");
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeObject = go;
        EnsureSerializedObjects();
    }

    private static T CreateAsset<T>(string folder, string fileName) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName + ".asset");
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static NpcDialogueAgentConfig FindConfigByTemplate(NpcPromptTemplate template)
    {
        if (template == null) return null;

        string[] guids = AssetDatabase.FindAssets("t:NpcDialogueAgentConfig");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var config = AssetDatabase.LoadAssetAtPath<NpcDialogueAgentConfig>(path);
            if (config != null && config.promptTemplate == template)
                return config;
        }

        return null;
    }

    private static NpcDialogueAgentConfig FindConfigByProfile(NpcPersonalityProfile profile)
    {
        if (profile == null) return null;

        string[] guids = AssetDatabase.FindAssets("t:NpcDialogueAgentConfig");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var config = AssetDatabase.LoadAssetAtPath<NpcDialogueAgentConfig>(path);
            if (config != null && config.personalityProfile == profile)
                return config;
        }

        return null;
    }

    private static void SaveAll()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
