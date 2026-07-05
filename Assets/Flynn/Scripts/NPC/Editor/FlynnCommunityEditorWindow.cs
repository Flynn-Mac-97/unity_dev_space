using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


using Flynn.Core;
using Flynn.Npc;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc.Editor
{
    // DB-schema-aware community authoring window, styled with the Flynn UI draft
    // palette (tokens.uss: flat black, 1px white outlines, square corners). Edits
    // the unified entity model (community / things / NPCs / signals / knowledge)
    // that the runtime mirrors into LiteDB. Authors against the canonical island
    // JSON; the runtime importer embeds + loads it. Shows reference dropdowns and a
    // "referenced by" view so links between world-things, NPCs, signals and
    // knowledge are visible while authoring.
    public class FlynnCommunityEditorWindow : EditorWindow
    {
        private const string k_IslandsRoot = "Assets/Flynn/Configs/NPC/Islands";
        private const string k_TokensUss   = "Assets/Flynn/UI/Styles/tokens.uss";
        private enum Tab { Community, Things, Npcs, Signals }

        private string[] _islandFiles = new string[0];
        private string[] _islandNames = new string[0];

        private IslandContent _content;
        private string _activePath;
        private bool _dirty;
        private Tab _tab = Tab.Community;
        private List<ValidationIssue> _issues = new List<ValidationIssue>();

        private DropdownField _islandPicker;
        private Label _statusLabel;
        private Label _dirtyLabel;
        private ScrollView _body;
        private readonly List<Button> _tabButtons = new List<Button>();

        [MenuItem("Flynn/NPC/Community Editor")]
        public static void Open()
        {
            var w = GetWindow<FlynnCommunityEditorWindow>("Community Editor");
            w.minSize = new Vector2(880f, 580f);
            w.Show();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            var tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_TokensUss);
            if (tokens != null) root.styleSheets.Add(tokens);
            root.AddToClassList("token-bg");
            root.style.flexGrow = 1f;
            Pad(root, 16);

            BuildHeader(root);
            Divider(root);
            BuildTabs(root);

            _body = new ScrollView();
            _body.style.flexGrow = 1f;
            root.Add(_body);

            RefreshIslandList();
            if (_islandFiles.Length > 0) Load(_islandFiles[0]);
            else Rebuild();
        }

        // ── Header / tabs ───────────────────────────────────────────────────────────

        private void BuildHeader(VisualElement root)
        {
            var bar = Row();
            bar.style.alignItems = Align.Center;

            var title = new Label("COMMUNITY EDITOR");
            title.AddToClassList("text-lg"); title.AddToClassList("token-text");
            title.style.marginRight = 16;
            bar.Add(title);

            _islandPicker = new DropdownField();
            _islandPicker.AddToClassList("token-text");
            _islandPicker.style.minWidth = 220;
            _islandPicker.RegisterValueChangedCallback(evt =>
            {
                int idx = Array.IndexOf(_islandNames, evt.newValue);
                if (idx < 0) return;
                if (_dirty && !EditorUtility.DisplayDialog("Unsaved changes", "Discard edits?", "Discard", "Cancel"))
                { _islandPicker.SetValueWithoutNotify(evt.previousValue); return; }
                Load(_islandFiles[idx]);
            });
            bar.Add(_islandPicker);

            bar.Add(Btn("Reload", () => { RefreshIslandList(); Load(_activePath); }));
            bar.Add(Btn("Validate", () => { Revalidate(); RefreshStatus(); }));
            bar.Add(Btn("Save JSON", Save));

            var spacer = new VisualElement(); spacer.style.flexGrow = 1f; bar.Add(spacer);

            _dirtyLabel = new Label(""); _dirtyLabel.AddToClassList("token-text-faint"); _dirtyLabel.style.marginRight = 10;
            bar.Add(_dirtyLabel);
            _statusLabel = new Label(""); _statusLabel.AddToClassList("token-text-muted");
            bar.Add(_statusLabel);

            root.Add(bar);
        }

        private void BuildTabs(VisualElement root)
        {
            _tabButtons.Clear();
            var bar = Row();
            bar.style.marginTop = 6; bar.style.marginBottom = 6;
            foreach (Tab t in Enum.GetValues(typeof(Tab)))
            {
                var tab = t;
                var b = Btn(t.ToString(), () => { _tab = tab; SyncTabButtons(); Rebuild(); });
                _tabButtons.Add(b);
                bar.Add(b);
            }
            root.Add(bar);
            SyncTabButtons();
        }

        private void SyncTabButtons()
        {
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool active = i == (int)_tab;
                _tabButtons[i].EnableInClassList("btn-primary", active);
            }
        }

        // ── Load / save ───────────────────────────────────────────────────────────

        private void RefreshIslandList()
        {
            var files = new List<string>();
            if (Directory.Exists(k_IslandsRoot))
                foreach (var p in Directory.GetFiles(k_IslandsRoot, "*.json", SearchOption.TopDirectoryOnly))
                {
                    if (p.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase)) continue;
                    files.Add(p.Replace('\\', '/'));
                }
            _islandFiles = files.ToArray();
            _islandNames = _islandFiles.Select(Path.GetFileNameWithoutExtension).ToArray();
            if (_islandPicker != null) _islandPicker.choices = _islandNames.ToList();
        }

        private void Load(string path)
        {
            _activePath = path;
            _dirty = false;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { _content = null; Rebuild(); return; }
            try { _content = JsonUtility.FromJson<IslandContent>(File.ReadAllText(path)); }
            catch (Exception e) { _content = null; Debug.LogError("[CommunityEditor] parse failed: " + e.Message); }
            EnsureLists();
            if (_islandPicker != null) _islandPicker.SetValueWithoutNotify(Path.GetFileNameWithoutExtension(path));
            Revalidate();
            Rebuild();
            RefreshStatus();
        }

        private void Save()
        {
            if (_content == null || string.IsNullOrEmpty(_activePath)) return;
            Revalidate();
            if (_issues.Any(i => i.severity == ValidationSeverity.Error) &&
                !EditorUtility.DisplayDialog("Validation errors",
                    "This island has errors:\n\n" + IslandContentValidator.Summarize(_issues) + "\n\nSave anyway?", "Save", "Cancel"))
            { RefreshStatus(); return; }

            File.WriteAllText(_activePath, JsonUtility.ToJson(_content, true));
            AssetDatabase.ImportAsset(_activePath);
            _dirty = false;
            RefreshStatus();
            ShowNotification(new GUIContent("Saved " + Path.GetFileName(_activePath)));
        }

        private void Revalidate() => _issues = _content != null ? IslandContentValidator.Validate(_content) : new List<ValidationIssue>();

        private void RefreshStatus()
        {
            if (_dirtyLabel != null) _dirtyLabel.text = _dirty ? "● unsaved" : "";
            if (_statusLabel == null) return;
            _statusLabel.text = IslandContentValidator.Summarize(_issues);
            bool err = _issues.Any(i => i.severity == ValidationSeverity.Error);
            _statusLabel.EnableInClassList("token-danger", err);
            _statusLabel.EnableInClassList("token-muted", !err);
        }

        private void EnsureLists()
        {
            if (_content == null) return;
            _content.things  ??= new List<WorldThingContent>();
            _content.signals ??= new List<SignalContent>();
            _content.npcs    ??= new List<NpcContent>();
        }

        private void MarkDirty() { _dirty = true; RefreshStatus(); }

        // ── Body ───────────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (_body == null) return;
            _body.Clear();
            if (_content == null) { _body.Add(Muted("No island loaded.")); return; }
            switch (_tab)
            {
                case Tab.Community: BuildCommunity(); break;
                case Tab.Things: BuildThings(); break;
                case Tab.Npcs: BuildNpcs(); break;
                case Tab.Signals: BuildSignals(); break;
            }
        }

        private void BuildCommunity()
        {
            if (_content.community == null)
            {
                _body.Add(Btn("Add community block", () => { _content.community = new CommunityContent(); MarkDirty(); Rebuild(); }));
                return;
            }
            var c = _content.community;
            var p = Panel(); _body.Add(p);
            p.Add(TextRow("Community Id", c.communityId, v => c.communityId = v));
            p.Add(TextRow("Display Name", c.displayName, v => c.displayName = v));
            p.Add(AreaRow("Overview", c.overview, v => c.overview = v));
            p.Add(AreaRow("Current Situation", c.currentSituation, v => c.currentSituation = v));
            p.Add(TextRow("Shared Mood", c.sharedMood, v => c.sharedMood = v));
            p.Add(AreaRow("Culture", c.culture, v => c.culture = v));

            _body.Add(Header("COMMUNITY KNOWLEDGE"));
            BuildKnowledgeList(c.knowledge ??= new List<KnowledgeContent>(), "community");
        }

        private void BuildThings()
        {
            _body.Add(Btn("+ Add Thing", () => { _content.things.Add(new WorldThingContent { thingId = "new_thing" }); MarkDirty(); Rebuild(); }));
            for (int i = 0; i < _content.things.Count; i++)
            {
                var t = _content.things[i]; int idx = i;
                var fold = Fold(string.IsNullOrEmpty(t.displayName) ? t.thingId : t.displayName);
                var p = Panel(); fold.Add(p);
                p.Add(TextRow("Thing Id", t.thingId, v => t.thingId = v));
                p.Add(TextRow("Display Name", t.displayName, v => t.displayName = v));
                p.Add(AreaRow("Short Description", t.shortDescription, v => t.shortDescription = v));
                p.Add(ListRow("Aliases", t.aliases, l => t.aliases = l));
                p.Add(ListRow("Tags", t.tags, l => t.tags = l));

                var knowers = _content.npcs.Where(n => n.knowledge != null && n.knowledge.Any(k => k.thingId == t.thingId)).Select(n => n.npcId).ToList();
                if (_content.community?.knowledge != null && _content.community.knowledge.Any(k => k.thingId == t.thingId)) knowers.Add("community");
                var sigs = _content.signals.Where(s => s.thingId == t.thingId).Select(s => s.signalId).ToList();
                p.Add(Muted("known by: " + (knowers.Count > 0 ? string.Join(", ", knowers) : "—")));
                p.Add(Muted("signals: " + (sigs.Count > 0 ? string.Join(", ", sigs) : "—")));

                p.Add(Btn("Remove Thing", () => { _content.things.RemoveAt(idx); MarkDirty(); Rebuild(); }));
                _body.Add(fold);
            }
        }

        private void BuildNpcs()
        {
            _body.Add(Btn("+ Add NPC", () => { _content.npcs.Add(new NpcContent { npcId = "npc.new" }); MarkDirty(); Rebuild(); }));
            for (int i = 0; i < _content.npcs.Count; i++)
            {
                var n = _content.npcs[i]; int idx = i;
                var fold = Fold(string.IsNullOrEmpty(n.displayName) ? n.npcId : n.displayName);
                var p = Panel(); fold.Add(p);
                p.Add(TextRow("Npc Id", n.npcId, v => n.npcId = v));
                p.Add(TextRow("Display Name", n.displayName, v => n.displayName = v));
                p.Add(AreaRow("Role", n.role, v => n.role = v));
                p.Add(AreaRow("Speaking Style", n.speakingStyle, v => n.speakingStyle = v));
                p.Add(IntRow("Starting Trust", n.startingTrust, v => n.startingTrust = v));
                p.Add(IntRow("Trust To Share Secrets", n.trustToShareSecrets, v => n.trustToShareSecrets = v));
                p.Add(AreaRow("Fallback Reply", n.fallbackReply, v => n.fallbackReply = v));
                p.Add(ListRow("Personality Traits", n.personalityTraits, l => n.personalityTraits = l));
                p.Add(ListRow("Do Rules", n.doRules, l => n.doRules = l));
                p.Add(ListRow("Don't Rules", n.dontRules, l => n.dontRules = l));
                p.Add(ListRow("Capabilities", n.capabilities, l => n.capabilities = l));

                fold.Add(Header("KNOWLEDGE"));
                BuildKnowledgeListInto(fold, n.knowledge ??= new List<KnowledgeContent>(), n.npcId);

                fold.Add(Btn("Remove NPC", () => { _content.npcs.RemoveAt(idx); MarkDirty(); Rebuild(); }));
                _body.Add(fold);
            }
        }

        private void BuildSignals()
        {
            _body.Add(Btn("+ Add Signal", () => { _content.signals.Add(new SignalContent { signalId = "signal_new" }); MarkDirty(); Rebuild(); }));
            for (int i = 0; i < _content.signals.Count; i++)
            {
                var s = _content.signals[i]; int idx = i;
                var fold = Fold(s.signalId);
                var p = Panel(); fold.Add(p);
                p.Add(TextRow("Signal Id", s.signalId, v => s.signalId = v));
                p.Add(DropdownRow("Thing", ThingChoices(), string.IsNullOrEmpty(s.thingId) ? "(untied)" : s.thingId,
                    v => s.thingId = v == "(untied)" ? "" : v));
                p.Add(AreaRow("Description", s.description, v => s.description = v));
                p.Add(DropdownRow("Handler", IslandContentVocab.SignalHandlers.ToList(), s.handler, v => s.handler = v));
                p.Add(ToggleRow("Repeatable", s.repeatable, v => s.repeatable = v));
                p.Add(IntRow("Min Trust To Fire", s.minTrustToFire, v => s.minTrustToFire = v));
                p.Add(AreaRow("Payload JSON", s.payloadJson, v => s.payloadJson = v));
                p.Add(Btn("Remove Signal", () => { _content.signals.RemoveAt(idx); MarkDirty(); Rebuild(); }));
                _body.Add(fold);
            }
        }

        private void BuildKnowledgeList(List<KnowledgeContent> list, string owner) => BuildKnowledgeListInto(_body, list, owner);

        private void BuildKnowledgeListInto(VisualElement parent, List<KnowledgeContent> list, string owner)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var k = list[i]; int idx = i;
                var p = Panel(); p.AddToClassList("panel-tight"); p.style.marginBottom = 4;
                p.Add(TextRow("Id", k.id, v => k.id = v));
                p.Add(DropdownRow("Kind", IslandContentVocab.KnowledgeKinds.ToList(), k.kind, v => k.kind = v));
                p.Add(DropdownRow("About Thing", ThingChoices(), string.IsNullOrEmpty(k.thingId) ? "(untied)" : k.thingId,
                    v => k.thingId = v == "(untied)" ? "" : v));
                p.Add(IntRow("Reveal Trust", k.revealTrustThreshold, v => k.revealTrustThreshold = v));
                p.Add(AreaRow("Text", k.text, v => k.text = v));
                p.Add(Btn("Remove", () => { list.RemoveAt(idx); MarkDirty(); Rebuild(); }));
                parent.Add(p);
            }
            parent.Add(Btn("+ Add Knowledge", () => { list.Add(new KnowledgeContent { id = owner + "_k" + (list.Count + 1), kind = "fact" }); MarkDirty(); Rebuild(); }));
        }

        private List<string> ThingChoices()
        {
            var ids = new List<string> { "(untied)" };
            ids.AddRange(_content.things.Select(t => t.thingId).Where(id => !string.IsNullOrEmpty(id)));
            return ids;
        }

        // ── Styled control factories ────────────────────────────────────────────────

        private VisualElement TextRow(string label, string value, Action<string> set)
        {
            var f = new TextField(label) { value = value ?? "" };
            f.AddToClassList("token-text");
            f.labelElement.AddToClassList("token-text-muted");
            f.RegisterValueChangedCallback(e => { set(e.newValue); MarkDirty(); });
            return f;
        }

        private VisualElement AreaRow(string label, string value, Action<string> set)
        {
            var f = new TextField(label) { value = value ?? "", multiline = true };
            f.AddToClassList("token-text");
            f.labelElement.AddToClassList("token-text-muted");
            f.style.whiteSpace = WhiteSpace.Normal;
            f.RegisterValueChangedCallback(e => { set(e.newValue); MarkDirty(); });
            return f;
        }

        private VisualElement IntRow(string label, int value, Action<int> set)
        {
            var s = new SliderInt(label, 0, 100) { value = Mathf.Clamp(value, 0, 100), showInputField = true };
            s.AddToClassList("token-text");
            s.labelElement.AddToClassList("token-text-muted");
            s.RegisterValueChangedCallback(e => { set(e.newValue); MarkDirty(); });
            return s;
        }

        private VisualElement ToggleRow(string label, bool value, Action<bool> set)
        {
            var t = new Toggle(label) { value = value };
            t.AddToClassList("token-text");
            t.labelElement.AddToClassList("token-text-muted");
            t.RegisterValueChangedCallback(e => { set(e.newValue); MarkDirty(); });
            return t;
        }

        private VisualElement DropdownRow(string label, List<string> choices, string current, Action<string> set)
        {
            if (!choices.Contains(current) && !string.IsNullOrEmpty(current)) choices.Insert(0, current);
            var d = new DropdownField(label, choices, Mathf.Max(0, choices.IndexOf(current ?? (choices.Count > 0 ? choices[0] : ""))));
            d.AddToClassList("token-text");
            d.labelElement.AddToClassList("token-text-muted");
            d.RegisterValueChangedCallback(e => { set(e.newValue); MarkDirty(); });
            return d;
        }

        // Newline-per-item list editing.
        private VisualElement ListRow(string label, List<string> list, Action<List<string>> set)
        {
            list ??= new List<string>();
            var f = new TextField(label + " (one per line)") { value = string.Join("\n", list), multiline = true };
            f.AddToClassList("token-text");
            f.labelElement.AddToClassList("token-text-muted");
            f.RegisterValueChangedCallback(e =>
            {
                set(e.newValue.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList());
                MarkDirty();
            });
            return f;
        }

        private Foldout Fold(string title)
        {
            var f = new Foldout { text = title, value = false };
            f.AddToClassList("token-text");
            f.style.marginBottom = 4;
            return f;
        }

        private Button Btn(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            return b;
        }

        private Label Header(string text)
        {
            var l = new Label(text);
            l.AddToClassList("text-base"); l.AddToClassList("token-text");
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 8; l.style.marginBottom = 2;
            return l;
        }

        private Label Muted(string text)
        {
            var l = new Label(text); l.AddToClassList("token-text-muted"); l.AddToClassList("text-sm");
            return l;
        }

        private VisualElement Panel() { var v = new VisualElement(); v.AddToClassList("panel"); v.style.marginBottom = 6; return v; }
        private VisualElement Row() { var v = new VisualElement(); v.AddToClassList("row"); return v; }
        private void Divider(VisualElement root) { var d = new VisualElement(); d.AddToClassList("divider"); root.Add(d); }
        private static void Pad(VisualElement v, int p) { v.style.paddingLeft = p; v.style.paddingRight = p; v.style.paddingTop = p; v.style.paddingBottom = p; }
    }

}
