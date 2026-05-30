using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Toggleable in-game debug HUD that shows every step of the NPC LLM pipeline.
// Subscribes to LlmDebugBus. Drop this component on a GameObject with a UIDocument
// whose source asset is Assets/Flynn/UI/Screens/LlmDebugWindow/LlmDebugWindow.uxml.
[RequireComponent(typeof(UIDocument))]
public class LlmDebugWindowController : MonoBehaviour
{
    [Tooltip("Key that toggles the window in builds and play mode.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F9;

    [Tooltip("Show the window from the start.")]
    [SerializeField] private bool startVisible = false;

    [Tooltip("Maximum number of turns kept in the panel (oldest dropped).")]
    [SerializeField, Min(1)] private int maxTurns = 30;

    private VisualElement _root;
    private VisualElement _turnList;
    private Label _turnCount;
    private Button _clearButton;
    private Button _closeButton;
    private Label _hint;

    private readonly Dictionary<int, VisualElement> _turnBodies = new Dictionary<int, VisualElement>();
    private readonly List<int> _turnOrder = new List<int>();
    private bool _visible;

    private const string k_HiddenClass = "hidden";
    private const string k_TurnClass = "llm-debug-turn";
    private const string k_TurnHeaderClass = "llm-debug-turn-header";
    private const string k_TurnNumClass = "llm-debug-turn-num";
    private const string k_TurnInputClass = "llm-debug-turn-input";
    private const string k_TurnStateClass = "llm-debug-turn-state";
    private const string k_TurnBodyClass = "llm-debug-turn-body";
    private const string k_StageClass = "llm-debug-stage";
    private const string k_StageHeaderClass = "llm-debug-stage-header";
    private const string k_StageNameClass = "llm-debug-stage-name";
    private const string k_StageSummaryClass = "llm-debug-stage-summary";
    private const string k_StageMsClass = "llm-debug-stage-ms";
    private const string k_StageErrorClass = "llm-debug-stage-error";
    private const string k_StageExpandedHostClass = "llm-debug-stage-expanded";
    private const string k_ExpandedClass = "expanded";
    private const string k_BlockLabelClass = "llm-debug-block-label";
    private const string k_BlockTextClass = "llm-debug-block-text";

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null)
        {
            Debug.LogWarning("[LlmDebugWindow] UIDocument missing rootVisualElement.");
            return;
        }

        _root = doc.rootVisualElement.Q<VisualElement>("llm-debug-root");
        if (_root == null)
        {
            Debug.LogWarning("[LlmDebugWindow] Could not find #llm-debug-root in the UXML.");
            return;
        }

        _turnList = _root.Q<VisualElement>("turn-list");
        _turnCount = _root.Q<Label>("turn-count");
        _clearButton = _root.Q<Button>("clear-button");
        _closeButton = _root.Q<Button>("close-button");
        _hint = _root.Q<Label>("hint");

        if (_hint != null) _hint.text = $"{toggleKey} to toggle  |  Stage rows expand on click";

        if (_clearButton != null) _clearButton.clicked += ClearAll;
        if (_closeButton != null) _closeButton.clicked += () => SetVisible(false);

        LlmDebugBus.TurnStarted += OnTurnStarted;
        LlmDebugBus.StageCompleted += OnStageCompleted;
        LlmDebugBus.TurnCompleted += OnTurnCompleted;
        LlmDebugBus.Cleared += ClearAll;

        SetVisible(startVisible);
        UpdateCount();
    }

    private void OnDisable()
    {
        LlmDebugBus.TurnStarted -= OnTurnStarted;
        LlmDebugBus.StageCompleted -= OnStageCompleted;
        LlmDebugBus.TurnCompleted -= OnTurnCompleted;
        LlmDebugBus.Cleared -= ClearAll;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) SetVisible(!_visible);
    }

    public void Toggle() => SetVisible(!_visible);

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (_root == null) return;
        if (visible) _root.RemoveFromClassList(k_HiddenClass);
        else _root.AddToClassList(k_HiddenClass);
    }

    private void ClearAll()
    {
        if (_turnList == null) return;
        _turnList.Clear();
        _turnBodies.Clear();
        _turnOrder.Clear();
        UpdateCount();
    }

    private void OnTurnStarted(int turnNumber, string playerInput)
    {
        if (_turnList == null) return;
        if (_turnBodies.ContainsKey(turnNumber)) return;

        var turn = new VisualElement();
        turn.AddToClassList(k_TurnClass);

        var header = new VisualElement();
        header.AddToClassList(k_TurnHeaderClass);

        var numLabel = new Label($"T{turnNumber}");
        numLabel.AddToClassList(k_TurnNumClass);
        header.Add(numLabel);

        var inputLabel = new Label(Truncate(playerInput, 140));
        inputLabel.AddToClassList(k_TurnInputClass);
        header.Add(inputLabel);

        var stateLabel = new Label("...");
        stateLabel.AddToClassList(k_TurnStateClass);
        stateLabel.name = "turn-state";
        header.Add(stateLabel);

        var body = new VisualElement();
        body.AddToClassList(k_TurnBodyClass);

        turn.Add(header);
        turn.Add(body);

        _turnList.Add(turn);
        _turnBodies[turnNumber] = body;
        _turnOrder.Add(turnNumber);

        EnforceMax();
        UpdateCount();
    }

    private void OnStageCompleted(LlmDebugBus.StageEntry entry)
    {
        if (entry == null) return;
        if (!_turnBodies.TryGetValue(entry.turnNumber, out var body) || body == null) return;

        var stage = new VisualElement();
        stage.AddToClassList(k_StageClass);
        if (!string.IsNullOrEmpty(entry.error)) stage.AddToClassList(k_StageErrorClass);

        var header = new VisualElement();
        header.AddToClassList(k_StageHeaderClass);

        var nameLabel = new Label(entry.stage.ToString().ToUpperInvariant());
        nameLabel.AddToClassList(k_StageNameClass);
        header.Add(nameLabel);

        string summaryText = !string.IsNullOrEmpty(entry.error)
            ? "ERROR: " + Truncate(entry.error, 120)
            : Truncate(entry.parsedSummary ?? string.Empty, 160);
        var summaryLabel = new Label(summaryText);
        summaryLabel.AddToClassList(k_StageSummaryClass);
        header.Add(summaryLabel);

        var msLabel = new Label(entry.elapsedMs + " ms");
        msLabel.AddToClassList(k_StageMsClass);
        header.Add(msLabel);

        var expanded = new VisualElement();
        expanded.AddToClassList(k_StageExpandedHostClass);
        AddBlock(expanded, "SYSTEM PROMPT", entry.systemPrompt);

        string historyLabel = entry.chatHistoryTurns > 0
            ? "CHAT HISTORY (" + entry.chatHistoryTurns + " prior turn" + (entry.chatHistoryTurns == 1 ? "" : "s") + ")"
            : "CHAT HISTORY";
        AddBlock(expanded, historyLabel, entry.chatHistory);

        AddBlock(expanded, "USER / INPUT", entry.userPrompt);
        AddBlock(expanded, "RAW MODEL OUTPUT", entry.rawResponse);
        AddBlock(expanded, "PARSED", entry.parsedSummary);
        if (!string.IsNullOrEmpty(entry.error)) AddBlock(expanded, "ERROR", entry.error);

        stage.Add(header);
        stage.Add(expanded);

        header.RegisterCallback<ClickEvent>(_ =>
        {
            if (stage.ClassListContains(k_ExpandedClass)) stage.RemoveFromClassList(k_ExpandedClass);
            else stage.AddToClassList(k_ExpandedClass);
        });

        body.Add(stage);
    }

    private void OnTurnCompleted(LlmDebugBus.TurnSummary s)
    {
        if (s == null) return;
        if (!_turnBodies.TryGetValue(s.turnNumber, out var body) || body == null) return;
        var header = body.parent?.Q<Label>("turn-state");
        if (header != null) header.text = "done";

        var summary = new VisualElement();
        summary.AddToClassList(k_StageClass);

        var hdr = new VisualElement();
        hdr.AddToClassList(k_StageHeaderClass);
        var nameLabel = new Label("RESULT");
        nameLabel.AddToClassList(k_StageNameClass);
        hdr.Add(nameLabel);

        var summaryLabel = new Label(Truncate(s.finalReply ?? string.Empty, 160));
        summaryLabel.AddToClassList(k_StageSummaryClass);
        hdr.Add(summaryLabel);

        summary.Add(hdr);

        var expanded = new VisualElement();
        expanded.AddToClassList(k_StageExpandedHostClass);
        AddBlock(expanded, "FINAL REPLY", s.finalReply);
        summary.Add(expanded);

        hdr.RegisterCallback<ClickEvent>(_ =>
        {
            if (summary.ClassListContains(k_ExpandedClass)) summary.RemoveFromClassList(k_ExpandedClass);
            else summary.AddToClassList(k_ExpandedClass);
        });

        body.Add(summary);
    }

    private static void AddBlock(VisualElement parent, string label, string content)
    {
        if (string.IsNullOrEmpty(content)) return;
        var lbl = new Label(label);
        lbl.AddToClassList(k_BlockLabelClass);
        parent.Add(lbl);
        var txt = new Label(content);
        txt.AddToClassList(k_BlockTextClass);
        txt.selection.isSelectable = true;
        parent.Add(txt);
    }

    private void EnforceMax()
    {
        while (_turnOrder.Count > maxTurns)
        {
            int dropped = _turnOrder[0];
            _turnOrder.RemoveAt(0);
            if (_turnBodies.TryGetValue(dropped, out var body) && body != null)
            {
                var turn = body.parent;
                _turnList.Remove(turn);
            }
            _turnBodies.Remove(dropped);
        }
    }

    private void UpdateCount()
    {
        if (_turnCount == null) return;
        _turnCount.text = _turnOrder.Count == 1 ? "1 turn" : $"{_turnOrder.Count} turns";
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}
