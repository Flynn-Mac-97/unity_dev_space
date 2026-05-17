using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private Label _npcNameLabel;
    private Label _dialogueText;
    private VisualElement _npcPortrait;
    private TextField _playerInput;
    private Button _closeButton;
    private VisualElement _panel;

    private NpcDialogueData _current;
    private NpcDialogueAgentConfig _activeAgentConfig;
    private int _lineIndex;
    private bool _isBound;
    private bool _callbacksRegistered;
    private bool _isWaitingForReply;

    private readonly List<string> _recentTurns = new List<string>();
    private const int k_DefaultRecentTurnsLimit = 8;
    private const string k_DefaultSaveSlotId = "slot_0";
    private const string k_PlayerSpeaker = "Player";

    private string _activeNpcMemoryId;

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

        _activeAgentConfig = null;
        _activeNpcMemoryId = null;
        _isWaitingForReply = false;
        _recentTurns.Clear();
        _current = data;
        _lineIndex = 0;
        _npcNameLabel.text = data.npcName;
        _dialogueText.text = data.lines.Length > 0 ? data.lines[0] : string.Empty;
        ApplyPortraitSprite(null);
        _playerInput.value = string.Empty;
        _panel.RemoveFromClassList("hidden");
        Time.timeScale = 0f;
    }

    public void OpenAgent(NpcDialogueAgentConfig config, NpcDialogueData fallbackData)
    {
        if (config == null)
        {
            Open(fallbackData);
            return;
        }

        if (!TryBindUi()) return;

        _activeAgentConfig = config;
        _isWaitingForReply = false;
        _recentTurns.Clear();

        _current = fallbackData;
        _lineIndex = 0;
        _activeNpcMemoryId = ResolveNpcMemoryId(config, fallbackData);
        LoadPersistentMemory();

        string npcName = config.personalityProfile != null
            ? config.personalityProfile.GetSafeDisplayName()
            : (fallbackData != null ? fallbackData.npcName : "NPC");

        _npcNameLabel.text = npcName;
        _dialogueText.text = GetOpeningLine(fallbackData, npcName);
        ApplyPortraitSprite(config.personalityProfile != null ? config.personalityProfile.portraitSprite : null);
        _playerInput.value = string.Empty;
        _panel.RemoveFromClassList("hidden");
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!TryBindUi()) return;

        _isWaitingForReply = false;
        _panel.AddToClassList("hidden");
        Time.timeScale = 1f;
        _playerInput.value = string.Empty;
    }

    // Called by the send button or Enter key
    public void SubmitPlayerInput()
    {
        if (!TryBindUi()) return;
        if (_current == null && _activeAgentConfig == null) return;
        if (_isWaitingForReply) return;

        string input = _playerInput.value.Trim();
        if (string.IsNullOrEmpty(input)) return;

        if (TryHandleChatCommand(input))
        {
            _playerInput.value = string.Empty;
            return;
        }

        Debug.Log($"[Dialogue] Player said: {input}");
        _playerInput.value = string.Empty;

        if (_activeAgentConfig != null && _activeAgentConfig.useLocalModel)
        {
            StartCoroutine(HandleAgentTurn(input));
            return;
        }

        AdvanceLegacyDialogue();
    }

    private IEnumerator HandleAgentTurn(string playerInput)
    {
        _isWaitingForReply = true;
        _dialogueText.text = "Thinking...";

        AddTurn(k_PlayerSpeaker, playerInput);

        SceneLlmManager sceneLlm = SceneLlmManager.Instance != null
            ? SceneLlmManager.Instance
            : FindObjectOfType<SceneLlmManager>();

        if (sceneLlm == null || !sceneLlm.HasValidSettings())
        {
            string noManagerFallback = GetFallbackReply();
            ApplyNpcReply(noManagerFallback);
            _isWaitingForReply = false;
            yield break;
        }

        string memorySummary = BuildMemorySummary();
        string systemPrompt = BuildSystemPrompt(memorySummary);

        string reply = null;
        string error = null;

        yield return StartCoroutine(LocalLlmClient.GenerateReply(
            sceneLlm.sharedLocalModelSettings,
            systemPrompt,
            playerInput,
            (result, requestError) =>
            {
                reply = result;
                error = requestError;
            }));

        if (!string.IsNullOrWhiteSpace(error))
        {
            Debug.LogWarning($"[Dialogue] LLM request error: {error}");
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            reply = GetFallbackReply();
        }

        ApplyNpcReply(reply);
        _isWaitingForReply = false;
    }

    private void AdvanceLegacyDialogue()
    {
        _lineIndex++;
        if (_lineIndex < _current.lines.Length)
            _dialogueText.text = _current.lines[_lineIndex];
        else
            Close();
    }

    private string BuildSystemPrompt(string memorySummary)
    {
        if (_activeAgentConfig == null || _activeAgentConfig.promptTemplate == null)
            return "You are a helpful NPC. Reply in 1-4 lines as text only.";

        return _activeAgentConfig.promptTemplate.BuildAssembledPrompt(
            _activeAgentConfig.personalityProfile,
            memorySummary);
    }

    private string BuildMemorySummary()
    {
        if (_activeAgentConfig == null || _activeAgentConfig.memorySettings == null)
        {
            if (_recentTurns.Count == 0) return "None yet.";
            return string.Join("\n", _recentTurns);
        }

        NpcMemorySettings settings = _activeAgentConfig.memorySettings;
        NpcDialogueMemoryStore.NpcMemoryEntry memory =
            NpcDialogueMemoryStore.GetOrCreateMemory(GetActiveNpcMemoryId(), GetActiveSaveSlotId());

        var lines = new List<string>();

        if (memory.memoryFacts != null && memory.memoryFacts.Count > 0)
        {
            lines.Add("Known facts:");

            int factBudget = Mathf.Clamp(settings.injectedFacts, 1, Mathf.Max(1, settings.memoryFactsLimit));
            int factsStart = Mathf.Max(0, memory.memoryFacts.Count - factBudget);
            for (int i = factsStart; i < memory.memoryFacts.Count; i++)
                lines.Add("- " + memory.memoryFacts[i]);
        }

        IList<string> recentTurnsSource = memory.recentTurns != null && memory.recentTurns.Count > 0
            ? (IList<string>)memory.recentTurns
            : _recentTurns;

        if (recentTurnsSource.Count > 0)
        {
            if (lines.Count > 0)
                lines.Add(string.Empty);

            lines.Add("Recent turns:");

            int recentBudget = Mathf.Clamp(settings.injectedRecentTurns, 1, GetRecentTurnsCap());
            int turnsStart = Mathf.Max(0, recentTurnsSource.Count - recentBudget);
            for (int i = turnsStart; i < recentTurnsSource.Count; i++)
                lines.Add(recentTurnsSource[i]);
        }

        if (lines.Count == 0) return "None yet.";

        return string.Join("\n", lines);
    }

    private void AddTurn(string speaker, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        string speakerName = string.IsNullOrWhiteSpace(speaker) ? "Unknown" : speaker;
        string normalizedContent = content.Trim();

        _recentTurns.Add($"{speakerName}: {normalizedContent}");

        int cap = GetRecentTurnsCap();

        while (_recentTurns.Count > cap)
            _recentTurns.RemoveAt(0);

        if (_activeAgentConfig == null) return;

        string saveSlotId = GetActiveSaveSlotId();
        string npcMemoryId = GetActiveNpcMemoryId();

        NpcDialogueMemoryStore.AddTurn(npcMemoryId, speakerName, normalizedContent, cap, saveSlotId);

        if (string.Equals(speakerName, k_PlayerSpeaker))
        {
            string fact = TryExtractPlayerFact(normalizedContent);
            if (!string.IsNullOrWhiteSpace(fact))
            {
                NpcMemorySettings settings = _activeAgentConfig.memorySettings;
                int factsLimit = settings != null ? Mathf.Max(1, settings.memoryFactsLimit) : 10;
                int maxFactLength = settings != null ? Mathf.Max(40, settings.maxFactLength) : 160;
                NpcDialogueMemoryStore.AddFact(npcMemoryId, fact, factsLimit, maxFactLength, saveSlotId);
            }
        }
    }

    private void ApplyNpcReply(string reply)
    {
        string npcName = _npcNameLabel != null ? _npcNameLabel.text : "NPC";
        _dialogueText.text = reply;
        AddTurn(npcName, reply);
    }

    private string GetFallbackReply()
    {
        if (_activeAgentConfig != null)
        {
            if (_activeAgentConfig.personalityProfile != null)
            {
                var lines = _activeAgentConfig.personalityProfile.fallbackLines;
                if (lines != null && lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]))
                    return lines[0];
            }

            if (!string.IsNullOrWhiteSpace(_activeAgentConfig.fallbackReply))
                return _activeAgentConfig.fallbackReply;
        }

        if (_current != null && _current.lines != null && _current.lines.Length > 0)
            return _current.lines[0];

        return "I am listening, but I need a moment.";
    }

    private static string GetOpeningLine(NpcDialogueData fallbackData, string npcName)
    {
        if (fallbackData != null && fallbackData.lines != null && fallbackData.lines.Length > 0)
            return fallbackData.lines[0];

        return string.Format("{0} is listening.", npcName);
    }

    private void ApplyPortraitSprite(Sprite portraitSprite)
    {
        if (_npcPortrait == null) return;

        if (portraitSprite == null)
        {
            _npcPortrait.style.backgroundImage = new StyleBackground((Texture2D)null);
            _npcPortrait.AddToClassList("hidden");
            return;
        }

        _npcPortrait.style.backgroundImage = new StyleBackground(portraitSprite);
        _npcPortrait.RemoveFromClassList("hidden");
    }

    private void LoadPersistentMemory()
    {
        _recentTurns.Clear();

        if (_activeAgentConfig == null) return;

        NpcDialogueMemoryStore.NpcMemoryEntry memory =
            NpcDialogueMemoryStore.GetOrCreateMemory(GetActiveNpcMemoryId(), GetActiveSaveSlotId());

        if (memory == null || memory.recentTurns == null || memory.recentTurns.Count == 0)
            return;

        int cap = GetRecentTurnsCap();
        int start = Mathf.Max(0, memory.recentTurns.Count - cap);
        for (int i = start; i < memory.recentTurns.Count; i++)
            _recentTurns.Add(memory.recentTurns[i]);
    }

    private int GetRecentTurnsCap()
    {
        if (_activeAgentConfig != null && _activeAgentConfig.memorySettings != null)
            return Mathf.Max(2, _activeAgentConfig.memorySettings.recentTurnsLimit);

        return k_DefaultRecentTurnsLimit;
    }

    private string GetActiveSaveSlotId()
    {
        SceneLlmManager sceneLlm = SceneLlmManager.Instance != null
            ? SceneLlmManager.Instance
            : FindObjectOfType<SceneLlmManager>();

        if (sceneLlm != null && !string.IsNullOrWhiteSpace(sceneLlm.saveSlotId))
            return sceneLlm.saveSlotId;

        return k_DefaultSaveSlotId;
    }

    private string GetActiveNpcMemoryId()
    {
        if (!string.IsNullOrWhiteSpace(_activeNpcMemoryId))
            return _activeNpcMemoryId;

        _activeNpcMemoryId = ResolveNpcMemoryId(_activeAgentConfig, _current);
        return _activeNpcMemoryId;
    }

    private static string ResolveNpcMemoryId(NpcDialogueAgentConfig config, NpcDialogueData fallbackData)
    {
        if (config != null && config.personalityProfile != null)
        {
            string profileId = config.personalityProfile.GetSafeNpcId();
            if (!string.IsNullOrWhiteSpace(profileId))
                return profileId;
        }

        if (fallbackData != null && !string.IsNullOrWhiteSpace(fallbackData.npcName))
        {
            string normalizedName = fallbackData.npcName.Trim().ToLowerInvariant().Replace(' ', '.');
            return "npc." + normalizedName;
        }

        return "npc.unknown";
    }

    private static string TryExtractPlayerFact(string playerText)
    {
        if (string.IsNullOrWhiteSpace(playerText)) return null;

        string trimmed = playerText.Trim();
        if (trimmed.Length < 12) return null;

        if (trimmed.Contains("?")) return null;

        string lowered = trimmed.ToLowerInvariant();
        if (lowered.StartsWith("remember "))
            return trimmed.Substring(9).Trim();

        return trimmed;
    }

    private bool TryHandleChatCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (input[0] != '/') return false;

        string command = input.Trim().ToLowerInvariant();

        if (command == "/save")
        {
            if (_activeAgentConfig == null)
            {
                _dialogueText.text = "Save is only available in agent dialogue.";
                return true;
            }

            string saveSlotId = GetActiveSaveSlotId();
            NpcDialogueMemoryStore.Save(saveSlotId);
            _dialogueText.text = string.Format("Memory saved to slot '{0}'.", saveSlotId);
            return true;
        }

        if (command == "/clear")
        {
            if (_activeAgentConfig == null)
            {
                _dialogueText.text = "Clear is only available in agent dialogue.";
                return true;
            }

            string npcMemoryId = GetActiveNpcMemoryId();
            string saveSlotId = GetActiveSaveSlotId();
            bool removed = NpcDialogueMemoryStore.ClearNpcMemory(npcMemoryId, saveSlotId, true);
            _recentTurns.Clear();

            _dialogueText.text = removed
                ? "Memory cleared for this NPC in the active slot."
                : "No saved memory found for this NPC in the active slot.";

            return true;
        }

        if (command == "/help")
        {
            _dialogueText.text = "Commands: /save, /clear";
            return true;
        }

        _dialogueText.text = "Unknown command. Try /help.";
        return true;
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
        _npcPortrait  = _root.Q<VisualElement>("npc-portrait");
        _playerInput  = _root.Q<TextField>("player-input");
        _closeButton  = _root.Q<Button>("close-button");
        Button sendButton = _root.Q<Button>("send-button");

        if (!HasAllRequiredElements(sendButton))
        {
            BuildFallbackUi();

            _panel        = _root.Q<VisualElement>("dialogue-panel");
            _npcNameLabel = _root.Q<Label>("npc-name");
            _dialogueText = _root.Q<Label>("dialogue-text");
            _npcPortrait  = _root.Q<VisualElement>("npc-portrait");
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
            _playerInput.RegisterCallback<KeyDownEvent>(OnPlayerInputKeyDown);
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

        VisualElement contentRow = new VisualElement();
        contentRow.AddToClassList("dialogue-content-row");

        VisualElement portrait = new VisualElement { name = "npc-portrait" };
        portrait.AddToClassList("npc-portrait");
        portrait.AddToClassList("hidden");

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

        contentRow.Add(portrait);
        contentRow.Add(dialogueText);

        panel.Add(npcName);
        panel.Add(contentRow);
        panel.Add(inputRow);
        panel.Add(closeButton);

        _root.Add(panel);
    }

    private void OnPlayerInputKeyDown(KeyDownEvent evt)
    {
        if (evt == null) return;

        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            return;

        SubmitPlayerInput();
        evt.StopPropagation();
        evt.PreventDefault();
    }
}
