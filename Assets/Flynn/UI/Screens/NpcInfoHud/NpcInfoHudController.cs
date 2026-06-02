using UnityEngine;
using UnityEngine.UIElements;
using Flynn.Npc.Memory;

[RequireComponent(typeof(UIDocument))]
public class NpcInfoHudController : MonoBehaviour
{
    [SerializeField] private float refreshInterval = 0.4f;
    [SerializeField] private int maxTopicChips = 4;

    [Tooltip("Optional. SO channel raised each dialogue turn with the recalled knowledge sent to the LLM. If unset, resolved from SceneLlmManager at runtime.")]
    [SerializeField] private RecalledKnowledgeChannel recalledChannel;

    private const int chipSummaryWords = 6;
    private const int chipSummaryChars = 40;

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _hudRoot;
    private Label _npcName, _roleTag, _hotkeyHint;
    private Label _trustValue, _affectionValue, _suspicionValue;
    private VisualElement _trustFill, _affectionFill, _suspicionFill;
    private VisualElement _knownTopics, _avoidedTopics, _fetchedKnowledge;
    private Label _avoidedHeader, _memoryCount, _lastTurn;
    private Label _debugTopic, _debugTrust, _debugAffection, _debugSuspicion;

    private NpcInteraction _focused;
    private NpcRelationshipState _focusedRelationship;
    private float _nextRefresh;
    private bool _channelSubscribed;

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        NpcInteraction.RangeChanged += HandleRangeChanged;
        ResolveChannel();
    }

    private void OnDisable()
    {
        NpcInteraction.RangeChanged -= HandleRangeChanged;
        UnsubscribeRelationship();
        UnsubscribeChannel();
    }

    // Resolve the recalled-knowledge channel (serialized field, else from the
    // scene manager) and subscribe once. Safe to call repeatedly.
    private void ResolveChannel()
    {
        if (recalledChannel == null)
        {
            var manager = SceneLlmManager.Instance != null ? SceneLlmManager.Instance : FindObjectOfType<SceneLlmManager>();
            if (manager != null) recalledChannel = manager.recalledKnowledgeChannel;
        }
        if (recalledChannel != null && !_channelSubscribed)
        {
            recalledChannel.OnRaised += Refresh;
            _channelSubscribed = true;
        }
    }

    private void UnsubscribeChannel()
    {
        if (recalledChannel != null && _channelSubscribed) recalledChannel.OnRaised -= Refresh;
        _channelSubscribed = false;
    }

    private void Start()
    {
        TryBind();
        SetVisible(false);
    }

    private bool TryBind()
    {
        if (_root != null) return true;
        if (_doc == null) _doc = GetComponent<UIDocument>();
        _root = _doc != null ? _doc.rootVisualElement : null;
        if (_root == null) return false;

        _hudRoot         = _root.Q<VisualElement>("hud-root");
        _npcName         = _root.Q<Label>("npc-name");
        _roleTag         = _root.Q<Label>("role-tag");
        _hotkeyHint      = _root.Q<Label>("hotkey-hint");
        _trustValue      = _root.Q<Label>("trust-value");
        _affectionValue  = _root.Q<Label>("affection-value");
        _suspicionValue  = _root.Q<Label>("suspicion-value");
        _trustFill       = _root.Q<VisualElement>("trust-fill");
        _affectionFill   = _root.Q<VisualElement>("affection-fill");
        _suspicionFill   = _root.Q<VisualElement>("suspicion-fill");
        _knownTopics     = _root.Q<VisualElement>("known-topics");
        _avoidedTopics   = _root.Q<VisualElement>("avoided-topics");
        _fetchedKnowledge = _root.Q<VisualElement>("fetched-knowledge");
        _avoidedHeader   = _root.Q<Label>("avoided-header");
        _memoryCount     = _root.Q<Label>("memory-count");
        _lastTurn        = _root.Q<Label>("last-turn");
        _debugTopic      = _root.Q<Label>("debug-topic");
        _debugTrust      = _root.Q<Label>("debug-trust");
        _debugAffection  = _root.Q<Label>("debug-affection");
        _debugSuspicion  = _root.Q<Label>("debug-suspicion");

        ResolveChannel();

        return _hudRoot != null;
    }

    private void HandleRangeChanged(NpcInteraction npc, bool inRange)
    {
        if (inRange) { Focus(npc); return; }
        if (_focused == npc) Focus(null);
    }

    private void Focus(NpcInteraction npc)
    {
        UnsubscribeRelationship();
        _focused = npc;

        if (npc == null) { SetVisible(false); return; }

        _focusedRelationship = npc.GetComponent<NpcRelationshipState>();
        if (_focusedRelationship != null) _focusedRelationship.OnChanged += Refresh;

        if (!TryBind()) return;
        SetVisible(true);
        Refresh();
    }

    private void UnsubscribeRelationship()
    {
        if (_focusedRelationship != null) _focusedRelationship.OnChanged -= Refresh;
        _focusedRelationship = null;
    }

    private void SetVisible(bool visible)
    {
        if (_hudRoot == null) return;
        if (visible) _hudRoot.RemoveFromClassList("hidden");
        else _hudRoot.AddToClassList("hidden");
    }

    private void Update()
    {
        if (_focused == null) return;
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        if (_focused == null || !TryBind()) return;

        var npc = _focused.ResolveNpcContent();
        string name = "Stranger";
        string role = "";
        string hotkey = "[E] Interact";

        if (npc != null)
        {
            name = string.IsNullOrWhiteSpace(npc.displayName) ? "Stranger" : npc.displayName.Trim();
            role = npc.role != null ? npc.role.Trim() : "";
        }

        if (_focused.Actions != null && _focused.Actions.Count > 0)
        {
            for (int i = 0; i < _focused.Actions.Count; i++)
            {
                var a = _focused.Actions[i];
                if (a == null) continue;
                string key = string.IsNullOrWhiteSpace(a.hotkeyHint) ? "E" : a.hotkeyHint;
                string label = string.IsNullOrWhiteSpace(a.actionLabel) ? a.name : a.actionLabel;
                hotkey = string.Format("[{0}] {1}", key, label);
                break;
            }
        }

        _npcName.text = name;
        _roleTag.text = role;
        _hotkeyHint.text = hotkey;

        ApplyRelationship(npc);
        ApplyTopics(npc);
        ApplyFetched(npc);
        ApplyMemory(npc);

        if (_debugTopic != null) _debugTopic.text = "topic: —";
    }

    private void ApplyRelationship(NpcContent npc)
    {
        int trust = 0;
        if (_focusedRelationship != null) trust = _focusedRelationship.trust;
        else if (npc != null) trust = npc.startingTrust;

        SetBar(_trustFill, _trustValue, trust);
        SetBar(_affectionFill, _affectionValue, 0);
        SetBar(_suspicionFill, _suspicionValue, 0);
    }

    private static void SetBar(VisualElement fill, Label value, int amount)
    {
        int clamped = Mathf.Clamp(amount, 0, 100);
        if (fill != null) fill.style.width = new Length(clamped, LengthUnit.Percent);
        if (value != null) value.text = clamped.ToString();
    }

    private void ApplyTopics(NpcContent npc)
    {
        _knownTopics.Clear();
        _avoidedTopics.Clear();
        _avoidedHeader.AddToClassList("hidden");

        if (npc == null || npc.knowledge == null || npc.knowledge.Count == 0) return;

        int trustNow = _focusedRelationship != null ? _focusedRelationship.trust : npc.startingTrust;
        int added = 0;
        int avoidedAdded = 0;

        for (int i = 0; i < npc.knowledge.Count; i++)
        {
            var entry = npc.knowledge[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.text)) continue;

            bool isAvoid = string.Equals(entry.kind, "avoid", System.StringComparison.OrdinalIgnoreCase);
            if (isAvoid)
            {
                if (avoidedAdded < maxTopicChips)
                {
                    _avoidedTopics.Add(MakeChip(entry.text, "avoided"));
                    avoidedAdded++;
                }
            }
            else if (added < maxTopicChips)
            {
                bool locked = entry.revealTrustThreshold > 0 && trustNow < entry.revealTrustThreshold;
                _knownTopics.Add(MakeChip(entry.text, locked ? "locked" : ""));
                added++;
            }
        }

        if (avoidedAdded > 0) _avoidedHeader.RemoveFromClassList("hidden");
    }

    // Shows the recalled knowledge/memories that were sent to the LLM for the
    // last player input on the focused NPC, so designers can see what the model
    // actually had to work with. Empty/mismatched → a single faint "—" chip.
    private void ApplyFetched(NpcContent npc)
    {
        if (_fetchedKnowledge == null) return;
        _fetchedKnowledge.Clear();

        string npcId = npc != null ? npc.npcId : null;
        bool hasMatch = recalledChannel != null
            && !string.IsNullOrEmpty(npcId)
            && string.Equals(recalledChannel.LastNpcId, npcId, System.StringComparison.Ordinal)
            && recalledChannel.Last != null && recalledChannel.Last.Count > 0;

        if (!hasMatch)
        {
            _fetchedKnowledge.Add(MakeChip("—", "empty"));
            return;
        }

        var items = recalledChannel.Last;
        for (int i = 0; i < items.Count; i++)
        {
            var e = items[i];
            if (string.IsNullOrWhiteSpace(e.Text)) continue;
            string tooltip = string.Format("{0}\n[{1}/{2}] score {3:0.00}", e.Text.Trim(), e.Subject, e.Source, e.Score);
            _fetchedKnowledge.Add(MakeChip(e.Text, "fetched", tooltip));
        }
    }

    private static VisualElement MakeChip(string fullText, string extraClass)
    {
        return MakeChip(fullText, extraClass, fullText);
    }

    private static VisualElement MakeChip(string fullText, string extraClass, string tooltip)
    {
        var chip = new Label(Summarize(fullText));
        chip.tooltip = tooltip;
        chip.AddToClassList("chip");
        if (!string.IsNullOrEmpty(extraClass)) chip.AddToClassList(extraClass);
        return chip;
    }

    // First ~6 words or ~40 chars, whichever is shorter, with an ellipsis when
    // truncated. The full text stays available via the chip tooltip.
    private static string Summarize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string trimmed = text.Trim();

        string[] words = trimmed.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        string byWords = words.Length > chipSummaryWords
            ? string.Join(" ", words, 0, chipSummaryWords)
            : trimmed;

        string result = byWords;
        if (result.Length > chipSummaryChars) result = result.Substring(0, chipSummaryChars).TrimEnd();

        return result.Length < trimmed.Length ? result + "…" : result;
    }

    private void ApplyMemory(NpcContent npc)
    {
        string npcId = npc != null && !string.IsNullOrWhiteSpace(npc.npcId) ? npc.npcId : "npc.unknown";
        var manager = SceneLlmManager.Instance != null ? SceneLlmManager.Instance : FindObjectOfType<SceneLlmManager>();

        int factCount, turnCount;
        string lastTurn;

        // Prefer the live LiteDB (semantic memory). Fall back to the legacy
        // NpcMemoryStore SO only when semantic memory is unavailable, matching
        // the dialogue write path so the counter reflects where memories land.
        if (manager != null && manager.SemanticMemoryReady)
        {
            var db = manager.MemoryDb;
            factCount = db.CountMemories(npcId);
            turnCount = db.CountChatTurns(npcId);
            lastTurn = db.GetLastChatTurnText(npcId);
        }
        else
        {
            var store = manager != null ? manager.memoryStore : null;
            var memory = store != null ? store.GetOrCreate(npcId) : null;
            factCount = memory != null && memory.memoryFacts != null ? memory.memoryFacts.Count : 0;
            turnCount = memory != null && memory.recentTurns != null ? memory.recentTurns.Count : 0;
            lastTurn = turnCount > 0 ? memory.recentTurns[turnCount - 1] : "";
        }

        if (factCount == 0 && turnCount == 0)
            _memoryCount.text = "No memories yet.";
        else
            _memoryCount.text = string.Format("{0} memor{1} · {2} turn{3}",
                factCount, factCount == 1 ? "y" : "ies",
                turnCount, turnCount == 1 ? "" : "s");

        _lastTurn.text = lastTurn;
    }
}
