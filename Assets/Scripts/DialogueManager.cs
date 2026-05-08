using UnityEngine;
using UnityEngine.UIElements;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private Label _npcNameLabel;
    private Label _dialogueText;
    private TextField _playerInput;
    private Button _closeButton;
    private VisualElement _panel;

    private NpcDialogueData _current;
    private int _lineIndex;
    private bool _isBound;
    private bool _callbacksRegistered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple DialogueManager instances detected. Replacing previous instance.");
        }

        Instance = this;
    }

    private void Start()
    {
        TryBindUi();
    }

    public void Open(NpcDialogueData data)
    {
        if (data == null)
        {
            Debug.LogWarning("DialogueManager.Open called with null dialogue data.");
            return;
        }

        if (!TryBindUi()) return;

        _current = data;
        _lineIndex = 0;
        _npcNameLabel.text = data.npcName;
        _dialogueText.text = data.lines.Length > 0 ? data.lines[0] : string.Empty;
        _playerInput.value = string.Empty;
        _panel.RemoveFromClassList("hidden");
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!TryBindUi()) return;

        _panel.AddToClassList("hidden");
        Time.timeScale = 1f;
        _playerInput.value = string.Empty;
    }

    // Called by the send button or Enter key
    public void SubmitPlayerInput()
    {
        if (!TryBindUi()) return;
        if (_current == null) return;

        string input = _playerInput.value.Trim();
        if (string.IsNullOrEmpty(input)) return;

        Debug.Log($"[Dialogue] Player said: {input}");
        _playerInput.value = string.Empty;

        // Advance to next NPC line if available
        _lineIndex++;
        if (_lineIndex < _current.lines.Length)
            _dialogueText.text = _current.lines[_lineIndex];
        else
            Close();
    }

    private bool TryBindUi()
    {
        if (_isBound) return true;

        if (uiDocument == null)
        {
            Debug.LogError("DialogueManager is missing UIDocument reference.");
            return false;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogError("DialogueManager could not access rootVisualElement.");
            return false;
        }

        _panel        = _root.Q<VisualElement>("dialogue-panel");
        _npcNameLabel = _root.Q<Label>("npc-name");
        _dialogueText = _root.Q<Label>("dialogue-text");
        _playerInput  = _root.Q<TextField>("player-input");
        _closeButton  = _root.Q<Button>("close-button");
        Button sendButton = _root.Q<Button>("send-button");

        if (!HasAllRequiredElements(sendButton))
        {
            BuildFallbackUi();

            _panel        = _root.Q<VisualElement>("dialogue-panel");
            _npcNameLabel = _root.Q<Label>("npc-name");
            _dialogueText = _root.Q<Label>("dialogue-text");
            _playerInput  = _root.Q<TextField>("player-input");
            _closeButton  = _root.Q<Button>("close-button");
            sendButton    = _root.Q<Button>("send-button");

            if (!HasAllRequiredElements(sendButton))
            {
                Debug.LogError("Dialogue UI is missing required elements. Check names: dialogue-panel, npc-name, dialogue-text, player-input, close-button, send-button.");
                return false;
            }
        }

        if (!_callbacksRegistered)
        {
            _closeButton.RegisterCallback<ClickEvent>(_ => Close());
            sendButton.RegisterCallback<ClickEvent>(_ => SubmitPlayerInput());
            _callbacksRegistered = true;
        }

        _panel.AddToClassList("hidden");
        _isBound = true;
        return true;
    }

    private bool HasAllRequiredElements(Button sendButton)
    {
        return _panel != null
            && _npcNameLabel != null
            && _dialogueText != null
            && _playerInput != null
            && _closeButton != null
            && sendButton != null;
    }

    private void BuildFallbackUi()
    {
        VisualElement panel = new VisualElement { name = "dialogue-panel" };
        panel.AddToClassList("dialogue-panel");
        panel.AddToClassList("hidden");

        Label npcName = new Label("Stranger") { name = "npc-name" };
        npcName.AddToClassList("npc-name-badge");

        Label dialogueText = new Label("Hello, traveller.") { name = "dialogue-text" };
        dialogueText.AddToClassList("dialogue-text");

        VisualElement inputRow = new VisualElement();
        inputRow.AddToClassList("input-row");

        TextField playerInput = new TextField { name = "player-input" };
        playerInput.AddToClassList("player-input");

        Button sendButton = new Button { name = "send-button", text = "Send" };
        sendButton.AddToClassList("send-button");

        Button closeButton = new Button { name = "close-button", text = "X" };
        closeButton.AddToClassList("close-button");

        inputRow.Add(playerInput);
        inputRow.Add(sendButton);

        panel.Add(npcName);
        panel.Add(dialogueText);
        panel.Add(inputRow);
        panel.Add(closeButton);

        _root.Add(panel);
    }
}
