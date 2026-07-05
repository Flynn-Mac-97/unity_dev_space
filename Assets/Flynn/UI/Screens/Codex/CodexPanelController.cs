using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Flynn.Npc;
using Flynn.Npc.Memory;

namespace Flynn.UI.Screens.Codex
{
    [RequireComponent(typeof(UIDocument))]
    public class CodexPanelController : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _codexRoot;
        private ScrollView _content;
        private VisualElement _npcSection, _worldSection, _tasksSection;
        private VisualElement _npcEntries, _worldEntries;
        private VisualElement _tasksEntries;
        private Label _secretsDisplay, _trustDisplay;
        private VisualElement _toast;

        private bool _isVisible;
        private const float k_ToastDuration = 2.5f;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            SubscribeToCodex();
        }

        private void Start()
        {
            TryBind();
            SetVisible(false);
            SubscribeToCodex();
        }

        private void SubscribeToCodex()
        {
            if (PlayerCodex.Instance == null) return;
            PlayerCodex.Instance.OnEntryAdded -= HandleEntryAdded;
            PlayerCodex.Instance.OnEntryTranslated -= HandleEntryTranslated;
            PlayerCodex.Instance.OnEntryAdded += HandleEntryAdded;
            PlayerCodex.Instance.OnEntryTranslated += HandleEntryTranslated;
            ObjectiveTracker.OnObjectivesChanged -= HandleObjectivesChanged;
            ObjectiveTracker.OnObjectivesChanged += HandleObjectivesChanged;
            Debug.Log("[CodexPanel] Subscribed to PlayerCodex + ObjectiveTracker events");
        }

        private void OnDisable()
        {
            if (PlayerCodex.Instance != null)
            {
                PlayerCodex.Instance.OnEntryAdded -= HandleEntryAdded;
                PlayerCodex.Instance.OnEntryTranslated -= HandleEntryTranslated;
            }
            ObjectiveTracker.OnObjectivesChanged -= HandleObjectivesChanged;
        }

        private bool TryBind()
        {
            if (_root != null) return true;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            _root = _doc != null ? _doc.rootVisualElement : null;
            if (_root == null) return false;

            _codexRoot      = _root.Q<VisualElement>("codex-root");
            _content        = _root.Q<ScrollView>("codex-content");
            _npcSection     = _root.Q<VisualElement>("npc-section");
            _worldSection   = _root.Q<VisualElement>("world-section");
            _tasksSection   = _root.Q<VisualElement>("tasks-section");
            _npcEntries     = _root.Q<VisualElement>("npc-entries");
            _worldEntries   = _root.Q<VisualElement>("world-entries");
            _tasksEntries   = _root.Q<VisualElement>("tasks-entries");
            _secretsDisplay = _root.Q<Label>("secrets-display");
            _trustDisplay   = _root.Q<Label>("trust-display");
            _toast          = _root.Q<VisualElement>("codex-toast");

            var closeBtn = _root.Q<Button>("codex-close");
            if (closeBtn != null)
                closeBtn.RegisterCallback<ClickEvent>(_ => SetVisible(false));

            _root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape && _isVisible)
                {
                    SetVisible(false);
                    evt.StopPropagation();
                }
            });

            var tabNpc = _root.Q<Button>("tab-npc");
            var tabWorld = _root.Q<Button>("tab-world");
            var tabTasks = _root.Q<Button>("tab-tasks");
            if (tabNpc != null) tabNpc.RegisterCallback<ClickEvent>(_ => SwitchTab("npc"));
            if (tabWorld != null) tabWorld.RegisterCallback<ClickEvent>(_ => SwitchTab("world"));
            if (tabTasks != null) tabTasks.RegisterCallback<ClickEvent>(_ => SwitchTab("tasks"));

            return _codexRoot != null;
        }

        public void Toggle()
        {
            if (!TryBind()) return;
            SetVisible(!_isVisible);
        }

        private void SetVisible(bool visible)
        {
            if (!TryBind()) return;
            _isVisible = visible;
            if (visible)
            {
                _codexRoot.RemoveFromClassList("hidden");
                // Mark all entries as viewed when the codex is opened.
                PlayerCodex.Instance?.MarkAllEntriesViewed();
                Refresh();
            }
            else
            {
                _codexRoot.AddToClassList("hidden");
            }
        }

        private void SwitchTab(string tab)
        {
            if (!TryBind()) return;

            var tabNpc = _root.Q<Button>("tab-npc");
            var tabWorld = _root.Q<Button>("tab-world");
            var tabTasks = _root.Q<Button>("tab-tasks");

            tabNpc?.RemoveFromClassList("active");
            tabWorld?.RemoveFromClassList("active");
            tabTasks?.RemoveFromClassList("active");

            _npcSection.style.display = DisplayStyle.None;
            _worldSection.style.display = DisplayStyle.None;
            _tasksSection.style.display = DisplayStyle.None;

            switch (tab)
            {
                case "npc":
                    tabNpc?.AddToClassList("active");
                    _npcSection.style.display = DisplayStyle.Flex;
                    break;
                case "world":
                    tabWorld?.AddToClassList("active");
                    _worldSection.style.display = DisplayStyle.Flex;
                    break;
                case "tasks":
                    tabTasks?.AddToClassList("active");
                    _tasksSection.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        private void Refresh()
        {
            if (!TryBind()) return;
            PopulateNpcSection();
            PopulateWorldSection();
            PopulateTasksSection();
        }

        private int GetCurrentTrust()
        {
            var npcObj = FindFirstObjectByType<NpcInteraction>();
            if (npcObj != null)
            {
                var rel = npcObj.GetComponent<NpcRelationshipState>();
                if (rel != null) return rel.trust;
            }
            return 0;
        }

        private void PopulateNpcSection()
        {
            if (_npcEntries == null) return;
            _npcEntries.Clear();

            var codex = PlayerCodex.Instance;
            if (codex == null)
            {
                _npcEntries.Add(CreateEmptyEntry("Codex not initialized."));
                return;
            }

            string npcId = "npc.transmitter_station";
            int total = codex.GetTotalSecretCount(npcId);
            int revealed = codex.GetRevealedSecretCount(npcId);

            if (_secretsDisplay != null)
                _secretsDisplay.text = $"Secrets {revealed}/{total}";

            int trust = GetCurrentTrust();
            if (_trustDisplay != null)
                _trustDisplay.text = $"Trust {trust}";

            var entries = codex.GetEntriesForNpc(npcId);
            if (entries.Count == 0)
            {
                _npcEntries.Add(CreateEmptyEntry("You haven't learned anything yet. Talk to The Station."));
            }
            else
            {
                // Reverse order — newest first.
                entries.Reverse();
                foreach (var entry in entries)
                    _npcEntries.Add(CreateEntryElement(entry));
            }

            var locked = codex.GetLockedSecrets(npcId, trust);
            foreach (var lk in locked)
                _npcEntries.Add(CreateLockedEntry(lk, trust));
        }

        private void PopulateWorldSection()
        {
            if (_worldEntries == null) return;
            _worldEntries.Clear();

            var codex = PlayerCodex.Instance;
            if (codex == null) return;

            var entries = codex.GetAllEntries();
            bool any = false;
            foreach (var entry in entries)
            {
                if (entry.SourceNpcId != "scan" && string.IsNullOrEmpty(entry.ThingId)) continue;
                _worldEntries.Add(CreateEntryElement(entry));
                any = true;
            }

            if (!any)
                _worldEntries.Add(CreateEmptyEntry("No world scans yet. Explore the island."));
        }

        private void PopulateTasksSection()
        {
            if (_tasksEntries == null) return;
            _tasksEntries.Clear();

            var tracker = ObjectiveTracker.Instance;
            if (tracker == null)
            {
                _tasksEntries.Add(CreateEmptyEntry("No active tasks."));
                return;
            }

            var objectives = tracker.GetAllObjectives();
            if (objectives.Count == 0)
            {
                _tasksEntries.Add(CreateEmptyEntry("No active tasks."));
                return;
            }

            foreach (var obj in objectives)
            {
                var row = new VisualElement();
                row.AddToClassList("codex-entry");

                var symbol = new Label(obj.State == ObjectiveTracker.ObjectiveState.Completed ? "✓" : "○");
                symbol.AddToClassList("codex-entry-symbol");
                if (obj.State == ObjectiveTracker.ObjectiveState.Completed)
                    symbol.style.color = new StyleColor(new Color(0.2f, 0.77f, 0.35f));
                row.Add(symbol);

                var body = new VisualElement();
                body.AddToClassList("codex-entry-body");

                var title = new Label(obj.Title);
                title.AddToClassList("codex-entry-title");
                if (obj.State == ObjectiveTracker.ObjectiveState.Completed)
                    title.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
                body.Add(title);

                row.Add(body);
                _tasksEntries.Add(row);
            }
        }

        private VisualElement CreateEntryElement(PlayerCodexEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("codex-entry");
            row.AddToClassList("clickable");

            var symbol = new Label(entry.IsEncrypted ? "▣" : "✓");
            symbol.AddToClassList("codex-entry-symbol");
            if (entry.IsEncrypted) symbol.AddToClassList("encrypted");
            row.Add(symbol);

            // NEW badge for unviewed entries.
            if (!entry.HasBeenViewed)
            {
                var badge = new Label("NEW");
                badge.AddToClassList("codex-new-badge");
                row.Add(badge);
            }

            var body = new VisualElement();
            body.AddToClassList("codex-entry-body");

            var title = new Label(string.IsNullOrEmpty(entry.Topic) ? "Unknown" : entry.Topic);
            title.AddToClassList("codex-entry-title");
            if (entry.IsEncrypted) title.AddToClassList("encrypted");
            body.Add(title);

            if (entry.IsEncrypted)
            {
                var fact = new Label(entry.EncryptedText ?? "???");
                fact.AddToClassList("codex-entry-fact");
                fact.AddToClassList("encrypted");
                body.Add(fact);

                var hint = new Label("→ Ask The Station to translate");
                hint.AddToClassList("codex-entry-hint");
                body.Add(hint);
            }
            else if (!string.IsNullOrEmpty(entry.FactText))
            {
                var fact = new Label(entry.FactText);
                fact.AddToClassList("codex-entry-fact");
                body.Add(fact);
            }

            row.Add(body);

            // Click → pre-fill dialogue input with "Tell me more about [topic]"
            string topic = entry.Topic;
            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (DialogueManager.Instance != null && !string.IsNullOrEmpty(topic))
                {
                    DialogueManager.Instance.SetPlayerInput($"Tell me more about {topic}");
                    Debug.Log($"[CodexPanel] Entry clicked: pre-filling '{topic}'");
                }
            });

            return row;
        }

        private VisualElement CreateLockedEntry(PlayerCodex.LockedKnowledge locked, int currentTrust)
        {
            var row = new VisualElement();
            row.AddToClassList("codex-entry");

            var symbol = new Label("?");
            symbol.AddToClassList("codex-entry-symbol");
            symbol.AddToClassList("locked");
            row.Add(symbol);

            var body = new VisualElement();
            body.AddToClassList("codex-entry-body");

            // Show the thing display name as a teaser instead of "???"
            string teaser = !string.IsNullOrEmpty(locked.DisplayName) ? locked.DisplayName : "something";
            var title = new Label($"??? — {teaser}");
            title.AddToClassList("codex-entry-title");
            title.AddToClassList("locked");
            body.Add(title);

            // Proximity indicator when within 5 trust of threshold
            int diff = locked.Threshold - currentTrust;
            string hint;
            if (diff <= 5)
                hint = $"Almost there — {currentTrust}/{locked.Threshold}";
            else
                hint = $"Reach trust {locked.Threshold} to unlock";
            var hintLabel = new Label(hint);
            hintLabel.AddToClassList("codex-entry-hint");
            if (diff <= 5) hintLabel.AddToClassList("codex-entry-hint-proximity");
            body.Add(hintLabel);

            row.Add(body);
            return row;
        }

        private VisualElement CreateEmptyEntry(string text)
        {
            var label = new Label(text);
            label.AddToClassList("codex-empty-text");
            return label;
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandleEntryAdded(PlayerCodexEntry entry)
        {
            Debug.Log($"[CodexPanel] HandleEntryAdded called: topic={entry.Topic}, isBound={_root != null}");
            ShowToast($"✦ New Knowledge: {(string.IsNullOrEmpty(entry.Topic) ? "Unknown" : entry.Topic)}");
            CodexAudio.PlayCodexChime();
            if (_isVisible) Refresh();
        }

        private void HandleEntryTranslated(PlayerCodexEntry entry)
        {
            if (_isVisible) Refresh();
            ShowToast($"✦ Translated: {entry.ThingId}");
        }

        private void HandleObjectivesChanged()
        {
            if (_isVisible) Refresh();
        }

        public void ShowToast(string text)
        {
            if (!TryBind())
            {
                Debug.LogWarning("[CodexPanel] ShowToast: TryBind failed");
                return;
            }
            if (_toast == null)
            {
                Debug.LogWarning("[CodexPanel] ShowToast: _toast element is null!");
                return;
            }
            Debug.Log($"[CodexPanel] ShowToast: displaying '{text}'");
            _toast.Clear();
            _toast.Add(new Label(text));
            _toast.RemoveFromClassList("hidden");
            _toast.style.display = DisplayStyle.Flex;

            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), k_ToastDuration);
        }

        private void HideToast()
        {
            if (_toast == null) return;
            _toast.AddToClassList("hidden");
            _toast.style.display = DisplayStyle.None;
        }
    }
}
