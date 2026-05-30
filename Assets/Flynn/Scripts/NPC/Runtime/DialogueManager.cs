using System;
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
    private int _turnCount;
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
        _turnCount = 0;
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
        OpenAgent(config, fallbackData, null);
    }

    // The relationship parameter is accepted for source compatibility with existing
    // callers (NpcInteraction passes one in) but is no longer used at runtime.
    public void OpenAgent(NpcDialogueAgentConfig config, NpcDialogueData fallbackData, NpcRelationshipState relationship)
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
        _turnCount = 0;

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

        SceneLlmManager sceneLlm = SceneLlmManager.Instance != null
            ? SceneLlmManager.Instance
            : FindObjectOfType<SceneLlmManager>();

        if (sceneLlm == null || !sceneLlm.HasValidSettings())
        {
            AddTurn(k_PlayerSpeaker, playerInput);
            ApplyNpcReply("[Fallback] " + GetFallbackReply());
            _isWaitingForReply = false;
            yield break;
        }

        int debugTurnNumber = _turnCount + 1;
        LlmDebugBus.BeginTurn(debugTurnNumber, playerInput);

        string systemPrompt = BuildSystemPrompt(sceneLlm);
        var priorTurns = BuildChatHistory();

        string reply = null;
        string error = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        yield return StartCoroutine(LocalLlmClient.GenerateReply(
            sceneLlm.sharedLocalModelSettings, systemPrompt, priorTurns, playerInput,
            (result, requestError) => { reply = result; error = requestError; }));
        sw.Stop();

        LlmDebugBus.RecordStage(new LlmDebugBus.StageEntry
        {
            turnNumber = debugTurnNumber,
            stage = LlmDebugBus.Stage.Dialogue,
            systemPrompt = systemPrompt,
            chatHistory = FormatChatHistoryForDebug(priorTurns),
            chatHistoryTurns = priorTurns != null ? priorTurns.Count : 0,
            userPrompt = playerInput,
            rawResponse = reply,
            parsedSummary = string.IsNullOrEmpty(reply) ? "(empty)" : reply,
            elapsedMs = sw.ElapsedMilliseconds,
            error = error
        });

        AddTurn(k_PlayerSpeaker, playerInput);

        if (!string.IsNullOrWhiteSpace(error))
            Debug.LogWarning($"[Dialogue] LLM request error: {error}");

        if (string.IsNullOrWhiteSpace(reply))
            reply = "[Fallback] " + GetFallbackReply();

        LlmDebugBus.EndTurn(new LlmDebugBus.TurnSummary
        {
            turnNumber = debugTurnNumber,
            playerInput = playerInput,
            finalReply = reply,
        });

        _turnCount++;

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

    // Single concatenated system prompt: scene-global rules + per-NPC persona + (optional)
    // facts the NPC knows + (optional) player profile block. One LLM call consumes this.
    private string BuildSystemPrompt(SceneLlmManager sceneLlm)
    {
        var sb = new System.Text.StringBuilder();

        if (sceneLlm != null && !string.IsNullOrWhiteSpace(sceneLlm.systemPrompt))
            sb.Append(sceneLlm.systemPrompt.Trim());

        var profile = _activeAgentConfig != null ? _activeAgentConfig.personalityProfile : null;
        if (_activeAgentConfig != null && !string.IsNullOrWhiteSpace(_activeAgentConfig.promptTemplate))
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(NpcPromptTokens.Apply(_activeAgentConfig.promptTemplate.Trim(), _activeAgentConfig));
        }
        else if (profile != null)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append("You are ").Append(profile.GetSafeDisplayName()).Append('.');
            if (!string.IsNullOrWhiteSpace(profile.roleDescription))
                sb.Append(' ').Append(profile.roleDescription.Trim());
        }

        string memorySummary = BuildMemorySummary(sceneLlm);
        if (!string.IsNullOrWhiteSpace(memorySummary) && memorySummary != "None yet.")
        {
            sb.Append("\n\n").Append(memorySummary);
        }

        if (sceneLlm != null && sceneLlm.playerProfile != null)
        {
            var p = sceneLlm.playerProfile;
            string playerName = p.GetSafeDisplayName();
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                sb.Append("\n\nThe player you are speaking to is named ").Append(playerName).Append('.');
                if (!string.IsNullOrWhiteSpace(p.persona))
                    sb.Append(' ').Append(p.persona.Trim());
            }
        }

        return sb.ToString();
    }

    private string BuildMemorySummary(SceneLlmManager sceneLlm)
    {
        // Only long-term facts go in the system prompt now.
        // Recent turns are sent to the model as real chat history via BuildChatHistory.
        NpcMemorySettings settings = GetSharedMemorySettings(sceneLlm);
        if (_activeAgentConfig == null || settings == null)
            return "None yet.";

        NpcDialogueMemoryStore.NpcMemoryEntry memory =
            NpcDialogueMemoryStore.GetOrCreateMemory(GetActiveNpcMemoryId(), GetActiveSaveSlotId());

        if (memory.memoryFacts == null || memory.memoryFacts.Count == 0)
            return "None yet.";

        var lines = new List<string> { "Known facts:" };
        int factBudget = Mathf.Max(1, settings.memoryFactsLimit);
        int factsStart = Mathf.Max(0, memory.memoryFacts.Count - factBudget);
        for (int i = factsStart; i < memory.memoryFacts.Count; i++)
            lines.Add("- " + memory.memoryFacts[i]);

        return string.Join("\n", lines);
    }

    private static NpcMemorySettings GetSharedMemorySettings(SceneLlmManager sceneLlm)
    {
        return sceneLlm != null ? sceneLlm.sharedMemorySettings : null;
    }

    // Render the chat-history window as a readable transcript for the debug panel
    // so designers can see exactly which prior turns the model is receiving with
    // this request.
    private static string FormatChatHistoryForDebug(List<LocalLlmClient.ChatTurn> priorTurns)
    {
        if (priorTurns == null || priorTurns.Count == 0) return "(no prior turns)";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < priorTurns.Count; i++)
        {
            var t = priorTurns[i];
            if (string.IsNullOrWhiteSpace(t.content)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(t.isAssistant ? "assistant: " : "user: ").Append(t.content.Trim());
        }
        return sb.Length == 0 ? "(no prior turns)" : sb.ToString();
    }

    private NpcMemorySettings GetSharedMemorySettings()
    {
        SceneLlmManager sceneLlm = SceneLlmManager.Instance != null
            ? SceneLlmManager.Instance
            : FindObjectOfType<SceneLlmManager>();
        return GetSharedMemorySettings(sceneLlm);
    }

    // Pulls the last N committed turns from memory and converts them into alternating
    // user/assistant chat messages, so the model has a proper conversational history
    // without bloating the system prompt as the chat grows.
    private List<LocalLlmClient.ChatTurn> BuildChatHistory()
    {
        var history = new List<LocalLlmClient.ChatTurn>();
        if (_activeAgentConfig == null) return history;

        NpcDialogueMemoryStore.NpcMemoryEntry memory =
            NpcDialogueMemoryStore.GetOrCreateMemory(GetActiveNpcMemoryId(), GetActiveSaveSlotId());

        if (memory.recentTurns == null || memory.recentTurns.Count == 0) return history;

        int windowSize = GetChatHistoryWindow();
        int start = Mathf.Max(0, memory.recentTurns.Count - windowSize);

        for (int i = start; i < memory.recentTurns.Count; i++)
        {
            string line = memory.recentTurns[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            int sep = line.IndexOf(": ", System.StringComparison.Ordinal);
            if (sep <= 0) continue;

            string speaker = line.Substring(0, sep);
            string content = line.Substring(sep + 2);
            if (string.IsNullOrWhiteSpace(content)) continue;

            history.Add(new LocalLlmClient.ChatTurn
            {
                isAssistant = !string.Equals(speaker, k_PlayerSpeaker, System.StringComparison.Ordinal),
                content = content,
            });
        }

        return history;
    }

    private int GetChatHistoryWindow()
    {
        NpcMemorySettings settings = GetSharedMemorySettings();
        if (settings != null)
            return Mathf.Max(2, settings.recentTurnsLimit);
        return k_DefaultRecentTurnsLimit;
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
                NpcMemorySettings settings = GetSharedMemorySettings();
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
        if (_playerInput != null) _playerInput.Focus();
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
        NpcMemorySettings settings = GetSharedMemorySettings();
        if (settings != null)
            return Mathf.Max(2, settings.recentTurnsLimit);

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
