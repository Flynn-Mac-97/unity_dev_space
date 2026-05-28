using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class NpcInfoHudController : MonoBehaviour
{
    [SerializeField] private float refreshInterval = 0.4f;
    [SerializeField] private int maxTopicChips = 4;

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _hudRoot;
    private Label _npcName, _roleTag, _hotkeyHint;
    private Label _trustValue, _affectionValue, _suspicionValue;
    private VisualElement _trustFill, _affectionFill, _suspicionFill;
    private VisualElement _knownTopics, _avoidedTopics;
    private Label _avoidedHeader, _memoryCount, _lastTurn;
    private Label _debugTopic, _debugTrust, _debugAffection, _debugSuspicion;

    private NpcInteraction _focused;
    private NpcRelationshipState _focusedRelationship;
    private NpcLlmResponseParser.ParsedTurn _lastUpdate;
    private NpcDialogueAgentConfig _lastUpdateConfig;
    private float _nextRefresh;

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        NpcInteraction.RangeChanged += HandleRangeChanged;
        DialogueManager.GameplayUpdateApplied += HandleGameplayUpdate;
    }

    private void OnDisable()
    {
        NpcInteraction.RangeChanged -= HandleRangeChanged;
        DialogueManager.GameplayUpdateApplied -= HandleGameplayUpdate;
        UnsubscribeRelationship();
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
        _avoidedHeader   = _root.Q<Label>("avoided-header");
        _memoryCount     = _root.Q<Label>("memory-count");
        _lastTurn        = _root.Q<Label>("last-turn");
        _debugTopic      = _root.Q<Label>("debug-topic");
        _debugTrust      = _root.Q<Label>("debug-trust");
        _debugAffection  = _root.Q<Label>("debug-affection");
        _debugSuspicion  = _root.Q<Label>("debug-suspicion");

        return _hudRoot != null;
    }

    private void HandleGameplayUpdate(NpcDialogueAgentConfig config, NpcLlmResponseParser.ParsedTurn update)
    {
        _lastUpdate = update;
        _lastUpdateConfig = config;
        if (_focused != null && _focused.AgentConfig == config) ApplyDebugUpdate();
    }

    private void HandleRangeChanged(NpcInteraction npc, bool inRange)
    {
        if (inRange)
        {
            Focus(npc);
            return;
        }

        if (_focused == npc) Focus(null);
    }

    private void Focus(NpcInteraction npc)
    {
        UnsubscribeRelationship();
        _focused = npc;

        if (npc == null)
        {
            SetVisible(false);
            return;
        }

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

        var config = _focused.AgentConfig;
        string name = "Stranger";
        string role = "";
        string hotkey = "[E] Interact";

        if (config != null && config.personalityProfile != null)
        {
            name = config.personalityProfile.GetSafeDisplayName();
        }

        if (config != null)
            role = FormatRoles(config.roles);

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

        ApplyRelationship(config);
        ApplyTopics(config);
        ApplyMemory(config);
        ApplyDebugUpdate();
    }

    private void ApplyDebugUpdate()
    {
        if (_debugTopic == null) return;

        bool relevant = _lastUpdateConfig != null && _focused != null && _focused.AgentConfig == _lastUpdateConfig;
        if (!relevant)
        {
            _debugTopic.text = "topic: —";
            SetDelta(_debugTrust,     "TRUST", 0);
            SetDelta(_debugAffection, "AFF",   0);
            SetDelta(_debugSuspicion, "SUSP",  0);
            return;
        }

        _debugTopic.text = "topic: " + (string.IsNullOrWhiteSpace(_lastUpdate.topic) ? "—" : _lastUpdate.topic);
        SetDelta(_debugTrust,     "TRUST", _lastUpdate.trustDelta);
        SetDelta(_debugAffection, "AFF",   _lastUpdate.affectionDelta);
        SetDelta(_debugSuspicion, "SUSP",  _lastUpdate.suspicionDelta);
    }

    private static void SetDelta(Label label, string prefix, int delta)
    {
        if (label == null) return;
        label.text = string.Format("{0} {1:+#;-#;0}", prefix, delta);
        label.RemoveFromClassList("pos");
        label.RemoveFromClassList("neg");
        if (delta > 0) label.AddToClassList("pos");
        else if (delta < 0) label.AddToClassList("neg");
    }

    private void ApplyRelationship(NpcDialogueAgentConfig config)
    {
        int trust = 0, affection = 0, suspicion = 0;

        if (_focusedRelationship != null)
        {
            trust = _focusedRelationship.trust;
            affection = _focusedRelationship.affection;
            suspicion = _focusedRelationship.suspicion;
        }
        else if (config != null && config.relationship != null)
        {
            trust = config.relationship.startingTrust;
            affection = config.relationship.startingAffection;
            suspicion = config.relationship.startingSuspicion;
        }

        SetBar(_trustFill, _trustValue, trust);
        SetBar(_affectionFill, _affectionValue, affection);
        SetBar(_suspicionFill, _suspicionValue, suspicion);
    }

    private static void SetBar(VisualElement fill, Label value, int amount)
    {
        int clamped = Mathf.Clamp(amount, 0, 100);
        if (fill != null) fill.style.width = new Length(clamped, LengthUnit.Percent);
        if (value != null) value.text = clamped.ToString();
    }

    private void ApplyTopics(NpcDialogueAgentConfig config)
    {
        _knownTopics.Clear();
        _avoidedTopics.Clear();
        _avoidedHeader.AddToClassList("hidden");

        if (config == null || config.knowledge == null) return;

        int trustNow = _focusedRelationship != null
            ? _focusedRelationship.trust
            : (config.relationship != null ? config.relationship.startingTrust : 0);

        var seen = new HashSet<string>();
        int added = 0;

        added += AddTopicChips(_knownTopics, config.knowledge.knownFacts, seen, trustNow, false, maxTopicChips - added);
        if (added < maxTopicChips)
            added += AddTopicChips(_knownTopics, config.knowledge.rumors, seen, trustNow, true, maxTopicChips - added);
        if (added < maxTopicChips)
            added += AddTopicChips(_knownTopics, config.knowledge.secrets, seen, trustNow, true, maxTopicChips - added);

        if (config.knowledge.avoidedTopics != null && config.knowledge.avoidedTopics.Count > 0)
        {
            int avoidedAdded = 0;
            for (int i = 0; i < config.knowledge.avoidedTopics.Count && avoidedAdded < maxTopicChips; i++)
            {
                var topic = config.knowledge.avoidedTopics[i];
                if (topic == null) continue;
                _avoidedTopics.Add(MakeChip(topic.GetSafeDisplayName(), "avoided"));
                avoidedAdded++;
            }
            if (avoidedAdded > 0) _avoidedHeader.RemoveFromClassList("hidden");
        }
    }

    private static int AddTopicChips(VisualElement target, List<NpcKnowledgeBase.KnowledgeEntry> entries,
        HashSet<string> seen, int trustNow, bool gateByReveal, int budget)
    {
        if (entries == null || budget <= 0) return 0;
        int added = 0;
        for (int i = 0; i < entries.Count && added < budget; i++)
        {
            var e = entries[i];
            if (e == null || e.topic == null) continue;
            string id = e.topic.GetSafeId();
            if (!seen.Add(id)) continue;

            bool locked = gateByReveal && !MeetsRevealCondition(e, trustNow);
            target.Add(MakeChip(e.topic.GetSafeDisplayName(), locked ? "locked" : ""));
            added++;
        }
        return added;
    }

    private static bool MeetsRevealCondition(NpcKnowledgeBase.KnowledgeEntry entry, int trustNow)
    {
        switch (entry.reveal)
        {
            case NpcKnowledgeBase.RevealCondition.Always: return true;
            case NpcKnowledgeBase.RevealCondition.TrustAtLeast: return trustNow >= entry.threshold;
            default: return false;
        }
    }

    private static VisualElement MakeChip(string text, string extraClass)
    {
        var chip = new Label(text);
        chip.AddToClassList("chip");
        if (!string.IsNullOrEmpty(extraClass)) chip.AddToClassList(extraClass);
        return chip;
    }

    private void ApplyMemory(NpcDialogueAgentConfig config)
    {
        string npcId = ResolveNpcId(config);
        string slot = ResolveSaveSlot();
        var memory = NpcDialogueMemoryStore.GetOrCreateMemory(npcId, slot);

        int factCount = memory != null && memory.memoryFacts != null ? memory.memoryFacts.Count : 0;
        int turnCount = memory != null && memory.recentTurns != null ? memory.recentTurns.Count : 0;

        if (factCount == 0 && turnCount == 0)
            _memoryCount.text = "No memories yet.";
        else
            _memoryCount.text = string.Format("{0} fact{1} · {2} turn{3}",
                factCount, factCount == 1 ? "" : "s",
                turnCount, turnCount == 1 ? "" : "s");

        string lastTurn = "";
        if (turnCount > 0) lastTurn = memory.recentTurns[turnCount - 1];
        _lastTurn.text = lastTurn;
    }

    private static string ResolveNpcId(NpcDialogueAgentConfig config)
    {
        if (config != null && config.personalityProfile != null)
            return config.personalityProfile.GetSafeNpcId();
        return "npc.unknown";
    }

    private static string ResolveSaveSlot()
    {
        var manager = SceneLlmManager.Instance != null ? SceneLlmManager.Instance : FindObjectOfType<SceneLlmManager>();
        if (manager != null && !string.IsNullOrWhiteSpace(manager.saveSlotId)) return manager.saveSlotId;
        return "slot_0";
    }

    private static string FormatRoles(NpcGameplayRoles roles)
    {
        if (roles == NpcGameplayRoles.None) return "";
        var parts = new List<string>();
        foreach (NpcGameplayRoles v in System.Enum.GetValues(typeof(NpcGameplayRoles)))
        {
            if (v == NpcGameplayRoles.None) continue;
            if ((roles & v) == v) parts.Add(v.ToString());
        }
        return string.Join(" · ", parts);
    }
}
