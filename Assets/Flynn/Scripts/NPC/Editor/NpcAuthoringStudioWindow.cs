using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class NpcAuthoringStudioWindow : EditorWindow
{
    private enum Tab { Identity, Knowledge, Triggers, Relationships, Ai, Dashboard }

    private const string UxmlPath = "Assets/Flynn/Scripts/NPC/Editor/NpcAuthoringStudio.uxml";
    private const string UssPath = "Assets/Flynn/Scripts/NPC/Editor/NpcAuthoringStudio.uss";
    private const string TokensPath = "Assets/Flynn/UI/Styles/tokens.uss";

    private NpcDialogueAgentConfig _config;
    private Tab _activeTab = Tab.Identity;

    private List<NpcDialogueAgentConfig> _configCache = new List<NpcDialogueAgentConfig>();
    private List<string> _configLabels = new List<string>();

    private DropdownField _picker;
    private IMGUIContainer _tabContent;
    private VisualElement _emptyState;
    private VisualElement _body;
    private Image _portraitImage;
    private Label _portraitEmpty;
    private Label _previewName;
    private Label _previewId;
    private Label _previewRole;
    private Label _previewStatus;

    private NpcIdentityTabView _identityView;
    private NpcKnowledgeTabView _knowledgeView;
    private NpcTriggersTabView _triggersView;
    private NpcRelationshipsTabView _relationshipsView;
    private NpcAiTabView _aiView;
    private NpcDashboardTabView _dashboardView;

    [MenuItem("Tools/Dialogue/NPC Crafting Studio")]
    public static void Open()
    {
        var window = GetWindow<NpcAuthoringStudioWindow>("NPC Crafting Studio");
        window.minSize = new Vector2(1080f, 620f);
        window.Show();
    }

    public static void OpenWithConfig(NpcDialogueAgentConfig config)
    {
        Open();
        var window = GetWindow<NpcAuthoringStudioWindow>("NPC Crafting Studio");
        window.SetConfig(config);
    }

    public static void OpenWithTemplate(NpcPromptTemplate template)
    {
        var found = FindConfigByMember(c => c.promptTemplate == template);
        if (found != null) OpenWithConfig(found);
    }

    public static void OpenWithProfile(NpcPersonalityProfile profile)
    {
        var found = FindConfigByMember(c => c.personalityProfile == profile);
        if (found != null) OpenWithConfig(found);
    }

    private void CreateGUI()
    {
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (uxml == null)
        {
            rootVisualElement.Add(new Label("Could not load NpcAuthoringStudio.uxml at " + UxmlPath));
            return;
        }
        uxml.CloneTree(rootVisualElement);

        var tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensPath);
        if (tokens != null) rootVisualElement.styleSheets.Add(tokens);

        var local = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        if (local != null) rootVisualElement.styleSheets.Add(local);

        _picker = rootVisualElement.Q<DropdownField>("NpcPicker");
        _tabContent = rootVisualElement.Q<IMGUIContainer>("TabContent");
        _emptyState = rootVisualElement.Q<VisualElement>("EmptyState");
        _body = rootVisualElement.Q<VisualElement>("Body");
        _portraitImage = rootVisualElement.Q<Image>("PortraitImage");
        _portraitEmpty = rootVisualElement.Q<Label>("PortraitEmpty");
        _previewName = rootVisualElement.Q<Label>("PreviewName");
        _previewId = rootVisualElement.Q<Label>("PreviewId");
        _previewRole = rootVisualElement.Q<Label>("PreviewRole");
        _previewStatus = rootVisualElement.Q<Label>("PreviewStatus");

        rootVisualElement.Q<Button>("BtnNew").clicked += OnNewClicked;
        rootVisualElement.Q<Button>("BtnDuplicate").clicked += OnDuplicateClicked;
        rootVisualElement.Q<Button>("BtnReveal").clicked += OnRevealClicked;
        rootVisualElement.Q<Button>("BtnRefresh").clicked += () => { RefreshConfigCache(); UpdatePreview(); };
        rootVisualElement.Q<Button>("BtnSave").clicked += OnSaveClicked;

        BindTab("TabIdentity", Tab.Identity);
        BindTab("TabKnowledge", Tab.Knowledge);
        BindTab("TabTriggers", Tab.Triggers);
        BindTab("TabRelationships", Tab.Relationships);
        BindTab("TabAi", Tab.Ai);
        BindTab("TabDashboard", Tab.Dashboard);

        _identityView = new NpcIdentityTabView();
        _knowledgeView = new NpcKnowledgeTabView();
        _triggersView = new NpcTriggersTabView();
        _relationshipsView = new NpcRelationshipsTabView();
        _aiView = new NpcAiTabView();
        _dashboardView = new NpcDashboardTabView();

        _tabContent.onGUIHandler = DrawActiveTab;

        _picker.RegisterValueChangedCallback(evt => OnPickerChanged(evt.newValue));

        RefreshConfigCache();
        if (_config == null && _configCache.Count > 0)
            SetConfig(_configCache[0]);
        else
            SyncUiToState();
    }

    private void BindTab(string name, Tab tab)
    {
        var btn = rootVisualElement.Q<Button>(name);
        if (btn != null)
            btn.clicked += () => { _activeTab = tab; HighlightActiveTab(); _tabContent.MarkDirtyRepaint(); };
    }

    private void HighlightActiveTab()
    {
        SetTabHighlight("TabIdentity", _activeTab == Tab.Identity);
        SetTabHighlight("TabKnowledge", _activeTab == Tab.Knowledge);
        SetTabHighlight("TabTriggers", _activeTab == Tab.Triggers);
        SetTabHighlight("TabRelationships", _activeTab == Tab.Relationships);
        SetTabHighlight("TabAi", _activeTab == Tab.Ai);
        SetTabHighlight("TabDashboard", _activeTab == Tab.Dashboard);
    }

    private void SetTabHighlight(string buttonName, bool active)
    {
        var btn = rootVisualElement.Q<Button>(buttonName);
        if (btn == null) return;
        if (active) btn.AddToClassList("tab-active");
        else btn.RemoveFromClassList("tab-active");
    }

    private void DrawActiveTab()
    {
        if (_config == null) return;
        switch (_activeTab)
        {
            case Tab.Identity: _identityView.Draw(_config); break;
            case Tab.Knowledge: _knowledgeView.Draw(_config); break;
            case Tab.Triggers: _triggersView.Draw(_config); break;
            case Tab.Relationships: _relationshipsView.Draw(_config); break;
            case Tab.Ai: _aiView.Draw(_config); break;
            case Tab.Dashboard: _dashboardView.Draw(_config); break;
        }
    }

    private void OnPickerChanged(string newValue)
    {
        if (newValue == null) return;
        for (int i = 0; i < _configLabels.Count; i++)
        {
            if (_configLabels[i] == newValue && i < _configCache.Count)
            {
                SetConfig(_configCache[i]);
                return;
            }
        }
    }

    private void OnNewClicked()
    {
        NpcNamePromptWindow.Open("Create New NPC", "NewNpc", name =>
        {
            var created = NpcSubAssetService.CreateNewConfigPacked(name);
            RefreshConfigCache();
            SetConfig(created);
            Selection.activeObject = created;
        });
    }

    private void OnDuplicateClicked()
    {
        if (_config == null) return;
        NpcNamePromptWindow.Open("Duplicate NPC", _config.name + "_Copy", name =>
        {
            var copy = NpcSubAssetService.DuplicateConfig(_config, name);
            RefreshConfigCache();
            if (copy != null) SetConfig(copy);
        });
    }

    private void OnRevealClicked()
    {
        if (_config == null) return;
        EditorGUIUtility.PingObject(_config);
        Selection.activeObject = _config;
    }

    private void OnSaveClicked()
    {
        if (_config != null)
        {
            EditorUtility.SetDirty(_config);
            if (_config.personalityProfile != null) EditorUtility.SetDirty(_config.personalityProfile);
        }
        AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent("Saved."));
        UpdatePreview();
    }

    private void SetConfig(NpcDialogueAgentConfig config)
    {
        _config = config;
        if (_config != null)
            NpcSubAssetService.EnsureMissingSubAssets(_config);
        SyncUiToState();
    }

    private void SyncUiToState()
    {
        bool has = _config != null;
        if (_emptyState != null) _emptyState.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
        if (_body != null) _body.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;

        if (_picker != null)
        {
            _picker.choices = new List<string>(_configLabels);
            if (has)
            {
                int idx = _configCache.IndexOf(_config);
                if (idx >= 0 && idx < _configLabels.Count)
                    _picker.SetValueWithoutNotify(_configLabels[idx]);
            }
        }

        HighlightActiveTab();
        UpdatePreview();
        _tabContent?.MarkDirtyRepaint();
    }

    private void UpdatePreview()
    {
        if (_previewName == null) return;

        if (_config == null)
        {
            _previewName.text = "—";
            _previewId.text = "—";
            _previewRole.text = "—";
            _previewStatus.text = "—";
            if (_portraitImage != null) _portraitImage.image = null;
            if (_portraitEmpty != null) _portraitEmpty.style.display = DisplayStyle.Flex;
            return;
        }

        var profile = _config.personalityProfile;
        _previewName.text = profile != null ? profile.GetSafeDisplayName() : "(no profile)";
        _previewId.text = profile != null ? profile.GetSafeNpcId() : "—";
        _previewRole.text = profile != null && !string.IsNullOrWhiteSpace(profile.roleDescription)
            ? profile.roleDescription
            : "(no role description)";

        Sprite portrait = profile != null ? profile.portraitSprite : null;
        if (_portraitImage != null && _portraitEmpty != null)
        {
            if (portrait != null)
            {
                _portraitImage.image = portrait.texture;
                _portraitEmpty.style.display = DisplayStyle.None;
            }
            else
            {
                _portraitImage.image = null;
                _portraitEmpty.style.display = DisplayStyle.Flex;
            }
        }

        var issues = NpcAuthoringValidator.Validate(_config);
        _previewStatus.text = NpcAuthoringValidator.FormatSummary(issues);
    }

    private void RefreshConfigCache()
    {
        _configCache = NpcSubAssetService.FindAllConfigs();
        _configCache.Sort((a, b) => string.Compare(GetLabel(a), GetLabel(b), StringComparison.OrdinalIgnoreCase));
        _configLabels = new List<string>(_configCache.Count);
        for (int i = 0; i < _configCache.Count; i++)
            _configLabels.Add(GetLabel(_configCache[i]));

        if (_picker != null)
        {
            _picker.choices = new List<string>(_configLabels);
            if (_config != null)
            {
                int idx = _configCache.IndexOf(_config);
                if (idx >= 0) _picker.SetValueWithoutNotify(_configLabels[idx]);
            }
        }
    }

    private static string GetLabel(NpcDialogueAgentConfig config)
    {
        if (config == null) return "(missing)";
        string display = config.personalityProfile != null
            ? config.personalityProfile.GetSafeDisplayName()
            : "Unassigned";
        return display + "  ·  " + config.name;
    }

    private static NpcDialogueAgentConfig FindConfigByMember(Func<NpcDialogueAgentConfig, bool> predicate)
    {
        var all = NpcSubAssetService.FindAllConfigs();
        for (int i = 0; i < all.Count; i++)
            if (predicate(all[i])) return all[i];
        return null;
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
        if (GUILayout.Button("Cancel")) { Close(); return; }
        if (GUILayout.Button("Create"))
        {
            if (string.IsNullOrWhiteSpace(_npcName))
            {
                EditorUtility.DisplayDialog("Create New NPC", "Please enter an NPC name.", "OK");
                return;
            }
            _onSubmit?.Invoke(_npcName.Trim());
            Close();
            return;
        }
        EditorGUILayout.EndHorizontal();
    }
}
