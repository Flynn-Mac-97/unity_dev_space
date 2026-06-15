using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using Flynn.Npc.Memory;
using Flynn.Events;

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
    private VisualElement _suggestionsRow;

    private string _activeNpcId;
    private NpcContent _activeNpc;
    private NpcRelationshipState _activeRelationship;
    private List<string> _availableSignalIdsThisTurn = new List<string>();

    private bool _isBound;
    private bool _callbacksRegistered;
    private bool _isWaitingForReply;

    private readonly List<string> _recentTurns = new List<string>();
    private int _turnCount;
    // Previous turn's topic + NPC reply, used to anchor semantic recall on
    // anaphoric follow-ups ("tell me more") that carry no topic on their own.
    private string _lastNpcReply;
    private string _lastTopic;
    private const int k_DefaultRecentTurnsLimit = 8;
    private const string k_DefaultSaveSlotId = "slot_0";
    private const string k_PlayerSpeaker = "Player";

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("Multiple DialogueManager instances detected. Replacing previous instance.");
        Instance = this;
    }

    private void Start() { TryBindUi(); }

    public void OpenAgent(string npcId, Sprite portraitOverride = null, NpcRelationshipState relationshipState = null)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning("DialogueManager.OpenAgent called with empty npcId.");
            return;
        }
        if (!TryBindUi()) return;

        var sceneLlm = ResolveSceneLlm();
        var hub = sceneLlm != null ? sceneLlm.islandContent : null;
        _activeNpc = hub != null ? hub.GetNpc(npcId) : null;
        if (_activeNpc == null)
            Debug.LogWarning("[Dialogue] No NpcContent in island for id '" + npcId + "'. Dialogue will use a generic fallback persona.");

        _activeNpcId = npcId;
        _activeRelationship = relationshipState;
        _isWaitingForReply = false;
        _recentTurns.Clear();
        _turnCount = 0;
        _lastNpcReply = null;
        _lastTopic = null;
        LoadPersistentMemory();

        string npcName = _activeNpc != null && !string.IsNullOrWhiteSpace(_activeNpc.displayName)
            ? _activeNpc.displayName.Trim()
            : "Stranger";
        _npcNameLabel.text = npcName;
        _dialogueText.text = string.Format("{0} is listening.", npcName);
        ApplyPortraitSprite(portraitOverride);
        _playerInput.value = string.Empty;
        ClearSuggestions();
        _panel.RemoveFromClassList("hidden");
        Time.timeScale = 0f;

        if (GameEventBus.Instance != null)
            GameEventBus.Instance.Publish(new NpcDialogueOpened(npcId));
    }

    public void Close()
    {
        if (!TryBindUi()) return;
        _isWaitingForReply = false;
        _panel.AddToClassList("hidden");
        Time.timeScale = 1f;
        _playerInput.value = string.Empty;
        ClearSuggestions();

        string closedId = _activeNpcId;
        _activeNpcId = null;
        if (GameEventBus.Instance != null && !string.IsNullOrWhiteSpace(closedId))
            GameEventBus.Instance.Publish(new NpcDialogueClosed(closedId));
    }

    public void SubmitPlayerInput()
    {
        if (!TryBindUi()) return;
        if (string.IsNullOrWhiteSpace(_activeNpcId)) return;
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

        var sceneLlm = ResolveSceneLlm();
        bool llmEnabled = sceneLlm != null && sceneLlm.HasValidSettings();
        if (llmEnabled)
        {
            StartCoroutine(HandleAgentTurn(input, sceneLlm));
            return;
        }

        AddTurn(k_PlayerSpeaker, input);
        ApplyNpcReply(GetFallbackReply());
    }

    private IEnumerator HandleAgentTurn(string playerInput, SceneLlmManager sceneLlm)
    {
        _isWaitingForReply = true;
        _dialogueText.text = "Thinking...";
        ClearSuggestions();

        if (string.IsNullOrEmpty(_activeNpcId) || sceneLlm == null || !sceneLlm.HasValidSettings())
        {
            AddTurn(k_PlayerSpeaker, playerInput);
            ApplyNpcReply("[Fallback] " + GetFallbackReply());
            _isWaitingForReply = false;
            yield break;
        }

        int debugTurnNumber = _turnCount + 1;
        LlmDebugBus.BeginTurn(debugTurnNumber, playerInput);

        // Resolve context for THIS turn — drives both prompt and signal allowlist.
        var ctx = ResolveContext(sceneLlm, playerInput);
        _availableSignalIdsThisTurn.Clear();
        if (ctx != null && ctx.availableSignals != null)
            foreach (var s in ctx.availableSignals) _availableSignalIdsThisTurn.Add(s.signalId);

        // Semantic recall: embed the player's input (+ resolved-thing context) and
        // pull the top-k most relevant memories/knowledge for this NPC. Null when
        // semantic memory is unavailable — BuildSystemPrompt then falls back to the
        // legacy keyword path.
        List<NpcMemoryDatabase.RecalledItem> recalled = null;
        if (sceneLlm.SemanticMemoryReady && !string.IsNullOrEmpty(_activeNpcId))
        {
            string queryText = BuildRecallQuery(playerInput, ctx);
            float[] queryVec = null;
            string embedError = null;
            yield return StartCoroutine(sceneLlm.Embedder.Embed(queryText, (v, e) => { queryVec = v; embedError = e; }));
            if (queryVec != null)
            {
                var es = sceneLlm.embeddingSettings;
                recalled = sceneLlm.MemoryDb.Recall(_activeNpcId, GetCurrentTrust(), queryVec, es.recallTopK, es.minSimilarity);
            }
            else Debug.LogWarning("[Dialogue] Query embed failed, skipping semantic recall: " + embedError);
        }

        // Surface what recall fetched (incl. null/empty) so the NPC Info HUD can
        // show designers which knowledge/memories went to the LLM this turn.
        if (sceneLlm.recalledKnowledgeChannel != null)
            sceneLlm.recalledKnowledgeChannel.Raise(_activeNpcId, recalled);

        string systemPrompt = BuildSystemPrompt(sceneLlm, ctx, recalled);
        var priorTurns = BuildChatHistory(sceneLlm);

        string reply = null;
        string error = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        yield return StartCoroutine(LocalLlmClient.GenerateReply(
            sceneLlm.GetActiveDialogueConfig(), systemPrompt, priorTurns, playerInput,
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

        string spokenLine;
        string[] suggestions = null;
        if (string.IsNullOrWhiteSpace(reply))
        {
            spokenLine = "[Fallback] " + GetFallbackReply();
        }
        else
        {
            var envelope = NpcReplyEnvelope.TryParse(reply);
            if (envelope != null && !string.IsNullOrWhiteSpace(envelope.reply))
            {
                spokenLine = envelope.reply.Trim();
                suggestions = envelope.suggested_player_replies;
                _lastTopic = envelope.topic;
                Debug.Log("[NPC JSON] " + envelope.ToDebugString());
                yield return StartCoroutine(WriteEnvelopeMemoryUpdates(envelope, sceneLlm));
                RaiseEnvelopeSignals(envelope, sceneLlm);
            }
            else
            {
                Debug.LogWarning("[NPC JSON] unparseable envelope. Raw=" + NpcReplyEnvelope.FlattenForLog(reply));
                spokenLine = reply.Trim();
            }
        }

        _lastNpcReply = spokenLine;

        LlmDebugBus.EndTurn(new LlmDebugBus.TurnSummary
        {
            turnNumber = debugTurnNumber,
            playerInput = playerInput,
            finalReply = spokenLine,
        });

        _turnCount++;

        ApplyNpcReply(spokenLine);
        ShowSuggestions(suggestions);
        _isWaitingForReply = false;
    }

    private ResolvedNpcContext ResolveContext(SceneLlmManager sceneLlm, string playerInput)
    {
        if (sceneLlm == null || sceneLlm.islandContent == null) return null;
        int trust = _activeRelationship != null ? _activeRelationship.trust
            : (_activeNpc != null ? _activeNpc.startingTrust : 0);
        Vector3? playerPos = sceneLlm.playerTransform != null
            ? sceneLlm.playerTransform.position
            : (Vector3?)null;
        return sceneLlm.Resolver.Resolve(
            npcId: _activeNpcId,
            currentTrust: trust,
            playerInput: playerInput,
            currentThingId: null,
            playerPosition: playerPos,
            proximityRadius: sceneLlm.proximityResolveRadius);
    }

    private string BuildSystemPrompt(SceneLlmManager sceneLlm, ResolvedNpcContext ctx, List<NpcMemoryDatabase.RecalledItem> recalled)
    {
        var sb = new System.Text.StringBuilder();
        LlmPromptConfig pc = sceneLlm != null ? sceneLlm.promptConfig : null;

        if (pc != null && !string.IsNullOrWhiteSpace(pc.systemPrompt))
            sb.Append(pc.systemPrompt.Trim());

        if (ctx != null && ctx.npc != null)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(IslandPromptBuilder.BuildPersonaBlock(ctx));
        }

        var hub = sceneLlm != null ? sceneLlm.islandContent : null;
        var community = hub != null ? hub.GetCommunity() : null;
        if (community != null)
        {
            string block = IslandPromptBuilder.BuildCommunityBlock(community);
            if (!string.IsNullOrEmpty(block)) sb.Append("\n\n").Append(block);
        }

        if (ctx != null)
        {
            string resolved = IslandPromptBuilder.BuildResolvedBlock(ctx);
            if (!string.IsNullOrEmpty(resolved)) sb.Append("\n\n").Append(resolved);
        }

        // Prefer semantic recall when available; otherwise fall back to the legacy
        // keyword-scored summary off the ScriptableObject store.
        string memorySummary = recalled != null
            ? RenderRecalledBlock(recalled)
            : BuildMemorySummary(sceneLlm, ctx);
        if (!string.IsNullOrWhiteSpace(memorySummary) && memorySummary != "None yet.")
            sb.Append("\n\n").Append(memorySummary);

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

        string jsonAddendum = (pc != null && !string.IsNullOrWhiteSpace(pc.jsonOutputAddendum))
            ? pc.jsonOutputAddendum
            : NpcReplyEnvelope.PromptAddendum;
        if (sb.Length > 0) sb.Append("\n\n");
        sb.Append(jsonAddendum);

        return sb.ToString();
    }

    // Inject only the most relevant long-term facts, not the last N. We store
    // more than we send: facts about the player / the relationship / what we've
    // disclosed are always relevant to whoever we're talking to; world/self
    // facts are preferred when they touch the thing currently being discussed.
    // This keeps the prompt focused on the resolver-selected topic instead of
    // dumping every accreted memory each turn.
    private string BuildMemorySummary(SceneLlmManager sceneLlm, ResolvedNpcContext ctx)
    {
        LlmPromptConfig settings = GetPromptConfig(sceneLlm);
        if (string.IsNullOrEmpty(_activeNpcId) || settings == null) return "None yet.";

        var memory = GetActiveMemoryEntry();
        if (memory == null || memory.memoryFacts == null || memory.memoryFacts.Count == 0) return "None yet.";

        var facts = memory.memoryFacts;

        // Relevance tokens from the resolved thing (display name + aliases).
        var tokens = new HashSet<string>();
        if (ctx != null && ctx.resolvedThing != null)
        {
            AddRelevanceTokens(tokens, ctx.resolvedThing.displayName);
            if (ctx.resolvedThing.aliases != null)
                foreach (var a in ctx.resolvedThing.aliases) AddRelevanceTokens(tokens, a);
        }

        // Score each fact, keep original index for recency tie-breaks.
        var scored = new List<KeyValuePair<int, int>>();
        for (int i = 0; i < facts.Count; i++)
        {
            string f = facts[i];
            if (string.IsNullOrWhiteSpace(f)) continue;
            string lower = f.ToLowerInvariant();
            int score = 0;
            foreach (var t in tokens) { if (lower.Contains(t)) { score += 2; break; } }
            if (lower.StartsWith("[player]") || lower.StartsWith("[relationship]") || lower.StartsWith("[disclosure]"))
                score += 1;
            scored.Add(new KeyValuePair<int, int>(i, score));
        }
        if (scored.Count == 0) return "None yet.";

        // Highest score first; newer (higher index) wins ties.
        scored.Sort((a, b) => b.Value != a.Value ? b.Value - a.Value : b.Key - a.Key);

        // Send fewer than we store — relevance filtering means we don't need the full cap.
        int take = Mathf.Min(Mathf.Clamp(settings.memoryFactsLimit, 1, 7), scored.Count);

        // Restore chronological order within the chosen set so it reads naturally.
        var chosen = new List<int>(take);
        for (int i = 0; i < take; i++) chosen.Add(scored[i].Key);
        chosen.Sort();

        var lines = new List<string> { "Known facts:" };
        foreach (var idx in chosen) lines.Add("- " + facts[idx]);
        return string.Join("\n", lines);
    }

    private int GetCurrentTrust()
    {
        if (_activeRelationship != null) return _activeRelationship.trust;
        return _activeNpc != null ? _activeNpc.startingTrust : 0;
    }

    // Query text for semantic recall: the player's input plus the resolved thing's
    // name/aliases so memories about whatever is being discussed score higher.
    private string BuildRecallQuery(string playerInput, ResolvedNpcContext ctx)
    {
        var sb = new System.Text.StringBuilder(playerInput);

        // Anaphoric follow-ups ("tell me more", "why?", "oh really") carry no topic
        // on their own, so the bare input embeds to a vague vector. Anchor recall
        // with the previous turn's topic + NPC reply so the query points at what's
        // actually being discussed. Skipped for substantive inputs, where the text
        // already recalls well and prior context would only add noise.
        if (IsFollowUp(playerInput))
        {
            if (!string.IsNullOrWhiteSpace(_lastTopic))
                sb.Append(' ').Append(_lastTopic);
            if (!string.IsNullOrWhiteSpace(_lastNpcReply))
                sb.Append(' ').Append(Truncate(_lastNpcReply, 200));
        }

        if (ctx != null && ctx.resolvedThing != null)
        {
            if (!string.IsNullOrWhiteSpace(ctx.resolvedThing.displayName))
                sb.Append(' ').Append(ctx.resolvedThing.displayName);
            if (ctx.resolvedThing.aliases != null)
                foreach (var a in ctx.resolvedThing.aliases)
                    if (!string.IsNullOrWhiteSpace(a)) sb.Append(' ').Append(a);
        }
        return sb.ToString();
    }

    // Short or referential player lines that lean on the prior turn for meaning.
    private static readonly string[] k_FollowUpCues =
    {
        "more", "it", "that", "this", "they", "them", "those", "these",
        "he", "she", "him", "her", "who", "really", "why"
    };

    private static bool IsFollowUp(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var parts = input.Trim().ToLowerInvariant()
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 3) return true;       // very short = needs context
        if (parts.Length > 8) return false;       // substantive = already specific
        foreach (var w in parts)
        {
            string ww = w.Trim('.', ',', '!', '?', ';', ':', '"', '\'');
            foreach (var cue in k_FollowUpCues)
                if (ww == cue) return true;
        }
        return false;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, max);
    }

    // Renders semantic-recall results into the "Known facts:" block. Memory items
    // keep their subject tag (e.g. "[player]"); authored knowledge renders plain.
    private static string RenderRecalledBlock(List<NpcMemoryDatabase.RecalledItem> recalled)
    {
        if (recalled == null || recalled.Count == 0) return "None yet.";
        var lines = new List<string> { "Known facts:" };
        foreach (var r in recalled)
        {
            if (string.IsNullOrWhiteSpace(r.Text)) continue;
            bool isKnowledge = string.Equals(r.Source, "knowledge", StringComparison.Ordinal);
            if (isKnowledge || string.IsNullOrWhiteSpace(r.Subject))
                lines.Add("- " + r.Text.Trim());
            else
                lines.Add("- [" + r.Subject + "] " + r.Text.Trim());
        }
        return lines.Count > 1 ? string.Join("\n", lines) : "None yet.";
    }

    private static void AddRelevanceTokens(HashSet<string> set, string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return;
        foreach (var w in s.ToLowerInvariant().Split(' '))
        {
            string t = w.Trim();
            if (t.Length >= 4) set.Add(t); // skip short stopwords like "the", "old"
        }
    }

    private static SceneLlmManager ResolveSceneLlm() =>
        SceneLlmManager.Instance != null ? SceneLlmManager.Instance : FindObjectOfType<SceneLlmManager>();

    private static LlmPromptConfig GetPromptConfig(SceneLlmManager sceneLlm) => sceneLlm != null ? sceneLlm.promptConfig : null;
    private LlmPromptConfig GetPromptConfig() => GetPromptConfig(ResolveSceneLlm());

    private NpcMemoryStore GetMemoryStore()
    {
        var s = ResolveSceneLlm();
        return s != null ? s.memoryStore : null;
    }

    private NpcMemoryStore.Entry GetActiveMemoryEntry()
    {
        if (string.IsNullOrEmpty(_activeNpcId)) return null;
        var store = GetMemoryStore();
        return store != null ? store.GetOrCreate(_activeNpcId) : null;
    }

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

    private List<LocalLlmClient.ChatTurn> BuildChatHistory(SceneLlmManager sceneLlm)
    {
        var history = new List<LocalLlmClient.ChatTurn>();
        if (string.IsNullOrEmpty(_activeNpcId)) return history;

        int windowSize = GetChatHistoryWindow();

        // DB-backed history when semantic memory is on; otherwise the SO store.
        if (sceneLlm != null && sceneLlm.SemanticMemoryReady)
        {
            var turns = new List<ChatTurnDoc>();
            foreach (var t in sceneLlm.MemoryDb.ChatTurns.Find(x => x.NpcId == _activeNpcId))
                turns.Add(t);
            turns.Sort((a, b) => a.TurnIndex.CompareTo(b.TurnIndex));
            int begin = Mathf.Max(0, turns.Count - windowSize);
            for (int i = begin; i < turns.Count; i++)
            {
                var t = turns[i];
                if (string.IsNullOrWhiteSpace(t.Content)) continue;
                history.Add(new LocalLlmClient.ChatTurn
                {
                    isAssistant = !string.Equals(t.Speaker, k_PlayerSpeaker, StringComparison.Ordinal),
                    content = t.Content,
                });
            }
            return history;
        }

        var memory = GetActiveMemoryEntry();
        if (memory == null || memory.recentTurns == null || memory.recentTurns.Count == 0) return history;

        int start = Mathf.Max(0, memory.recentTurns.Count - windowSize);
        for (int i = start; i < memory.recentTurns.Count; i++)
        {
            string line = memory.recentTurns[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            int sep = line.IndexOf(": ", StringComparison.Ordinal);
            if (sep <= 0) continue;

            string speaker = line.Substring(0, sep);
            string content = line.Substring(sep + 2);
            if (string.IsNullOrWhiteSpace(content)) continue;

            history.Add(new LocalLlmClient.ChatTurn
            {
                isAssistant = !string.Equals(speaker, k_PlayerSpeaker, StringComparison.Ordinal),
                content = content,
            });
        }

        return history;
    }

    private int GetChatHistoryWindow()
    {
        LlmPromptConfig settings = GetPromptConfig();
        if (settings != null) return Mathf.Max(2, settings.recentTurnsLimit);
        return k_DefaultRecentTurnsLimit;
    }

    private void AddTurn(string speaker, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        string speakerName = string.IsNullOrWhiteSpace(speaker) ? "Unknown" : speaker;
        string normalizedContent = content.Trim();

        _recentTurns.Add($"{speakerName}: {normalizedContent}");

        int cap = GetRecentTurnsCap();
        while (_recentTurns.Count > cap) _recentTurns.RemoveAt(0);

        if (string.IsNullOrEmpty(_activeNpcId)) return;

        // DB-backed chat log when semantic memory is on; otherwise the SO store.
        var sceneLlm = ResolveSceneLlm();
        if (sceneLlm != null && sceneLlm.SemanticMemoryReady)
        {
            sceneLlm.MemoryDb.InsertChatTurn(new ChatTurnDoc
            {
                NpcId = _activeNpcId,
                Speaker = speakerName,
                Content = normalizedContent,
                TurnIndex = sceneLlm.MemoryDb.NextTurnIndex(_activeNpcId),
            });
            return;
        }

        var store = GetMemoryStore();
        if (store == null) return;

        store.AddTurn(_activeNpcId, speakerName, normalizedContent, cap);
    }

    private IEnumerator WriteEnvelopeMemoryUpdates(NpcReplyEnvelope envelope, SceneLlmManager sceneLlm)
    {
        if (envelope == null || envelope.memory_updates == null) yield break;
        if (string.IsNullOrEmpty(_activeNpcId)) yield break;

        LlmPromptConfig settings = GetPromptConfig(sceneLlm);
        int maxFactLength = settings != null ? Mathf.Max(40, settings.maxFactLength) : 160;

        // Semantic path: embed each new fact and store it with its vector. Dedup is
        // a cosine check against existing same-NPC+subject memories (in TryInsertMemory).
        if (sceneLlm != null && sceneLlm.SemanticMemoryReady)
        {
            int turnNo = _turnCount + 1;
            for (int i = 0; i < envelope.memory_updates.Length; i++)
            {
                var update = envelope.memory_updates[i];
                if (update == null || string.IsNullOrWhiteSpace(update.fact)) continue;

                string subject = NormalizeMemorySubject(update.subject);
                string fact = update.fact.Trim();
                if (maxFactLength > 0 && fact.Length > maxFactLength) fact = fact.Substring(0, maxFactLength);

                float[] vec = null;
                string err = null;
                yield return StartCoroutine(sceneLlm.Embedder.Embed(fact, (v, e) => { vec = v; err = e; }));
                if (vec == null) { Debug.LogWarning("[Dialogue] Fact embed failed, not stored: " + err); continue; }

                sceneLlm.MemoryDb.TryInsertMemory(new MemoryDoc
                {
                    NpcId = _activeNpcId,
                    Subject = subject,
                    Text = fact,
                    Importance = 1f,
                    CreatedTurn = turnNo,
                    CreatedUtc = DateTime.UtcNow,
                    Source = MemorySource.Dialogue,
                    Embedding = vec,
                }, sceneLlm.embeddingSettings.dedupThreshold);
            }
            yield break;
        }

        // Fallback: legacy SO store with Jaccard dedup.
        var store = GetMemoryStore();
        if (store == null) yield break;
        int factsLimit = settings != null ? Mathf.Max(1, settings.memoryFactsLimit) : 10;
        for (int i = 0; i < envelope.memory_updates.Length; i++)
        {
            var update = envelope.memory_updates[i];
            if (update == null || string.IsNullOrWhiteSpace(update.fact)) continue;
            string subject = NormalizeMemorySubject(update.subject);
            string line = "[" + subject + "] " + update.fact.Trim();
            store.AddFact(_activeNpcId, line, factsLimit, maxFactLength);
        }
        store.Save();
    }

    // Validate each id in envelope.triggers_fired against THIS turn's signal allowlist
    // (so the model can't invent or replay non-repeatable signals), then raise survivors.
    private void RaiseEnvelopeSignals(NpcReplyEnvelope envelope, SceneLlmManager sceneLlm)
    {
        if (envelope == null || envelope.triggers_fired == null || envelope.triggers_fired.Length == 0) return;
        if (string.IsNullOrEmpty(_activeNpcId)) return;
        if (sceneLlm == null) return;

        var hub = sceneLlm.islandContent;
        var store = sceneLlm.memoryStore;
        var channel = sceneLlm.triggerChannel;

        for (int i = 0; i < envelope.triggers_fired.Length; i++)
        {
            string key = envelope.triggers_fired[i];
            if (string.IsNullOrWhiteSpace(key)) continue;
            string trimmed = key.Trim();

            if (!_availableSignalIdsThisTurn.Contains(trimmed))
            {
                Debug.LogWarning("[Dialogue] Model fired '" + trimmed + "' which was not in this turn's signal allowlist — dropped.");
                continue;
            }

            var signal = hub != null ? hub.GetSignal(trimmed) : null;
            if (signal == null)
            {
                Debug.LogWarning("[Dialogue] Signal '" + trimmed + "' not found in island content — dropped.");
                continue;
            }

            if (!signal.repeatable && store != null && store.WasTriggerFired(_activeNpcId, trimmed))
            {
                Debug.Log("[Dialogue] Signal '" + trimmed + "' already fired and not repeatable — dropped.");
                continue;
            }

            Debug.Log("[Dialogue] Signal fired: " + trimmed + " (by " + _activeNpcId + ")");
            channel?.Raise(trimmed, _activeNpcId, signal.handler, signal.payloadJson);
            store?.MarkTriggerFired(_activeNpcId, trimmed);
        }
        store?.Save();
    }

    private static string NormalizeMemorySubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return "world";
        string s = subject.Trim().ToLowerInvariant();
        if (s == "player" || s == "self" || s == "world" || s == "relationship" || s == "disclosure") return s;
        if (s == "npc" || s == "me") return "self";
        if (s == "user") return "player";
        if (s == "bond" || s == "rapport" || s == "trust") return "relationship";
        if (s == "told" || s == "shared" || s == "revealed") return "disclosure";
        return "world";
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
        if (_activeNpc != null && !string.IsNullOrWhiteSpace(_activeNpc.fallbackReply))
            return _activeNpc.fallbackReply;
        return "I am listening, but I need a moment.";
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
        if (string.IsNullOrEmpty(_activeNpcId)) return;

        var memory = GetActiveMemoryEntry();
        if (memory == null || memory.recentTurns == null || memory.recentTurns.Count == 0) return;

        int cap = GetRecentTurnsCap();
        int start = Mathf.Max(0, memory.recentTurns.Count - cap);
        for (int i = start; i < memory.recentTurns.Count; i++)
            _recentTurns.Add(memory.recentTurns[i]);
    }

    private int GetRecentTurnsCap()
    {
        LlmPromptConfig settings = GetPromptConfig();
        if (settings != null) return Mathf.Max(2, settings.recentTurnsLimit);
        return k_DefaultRecentTurnsLimit;
    }

    private string GetActiveSaveSlotId()
    {
        var s = ResolveSceneLlm();
        if (s != null && !string.IsNullOrWhiteSpace(s.saveSlotId)) return s.saveSlotId;
        return k_DefaultSaveSlotId;
    }

    private bool TryHandleChatCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (input[0] != '/') return false;

        string command = input.Trim().ToLowerInvariant();

        if (command == "/save")
        {
            if (string.IsNullOrEmpty(_activeNpcId))
            {
                _dialogueText.text = "Save is only available in agent dialogue.";
                return true;
            }
            var store = GetMemoryStore();
            if (store == null) { _dialogueText.text = "No memory store assigned on the scene LLM manager."; return true; }
            store.Save();
            _dialogueText.text = string.Format("Memory saved to slot '{0}'.", GetActiveSaveSlotId());
            return true;
        }

        if (command == "/clear")
        {
            if (string.IsNullOrEmpty(_activeNpcId))
            {
                _dialogueText.text = "Clear is only available in agent dialogue.";
                return true;
            }
            var sceneLlm = ResolveSceneLlm();
            bool removed = false;
            if (sceneLlm != null && sceneLlm.SemanticMemoryReady)
            {
                sceneLlm.MemoryDb.ClearNpc(_activeNpcId);
                removed = true;
            }
            var store = GetMemoryStore();
            if (store != null && store.ClearNpc(_activeNpcId)) removed = true;
            _recentTurns.Clear();
            _lastNpcReply = null;
            _lastTopic = null;
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
        if (uiDocument == null) { Debug.LogError("DialogueManager is missing UIDocument reference."); return false; }

        _root = uiDocument.rootVisualElement;
        if (_root == null) { Debug.LogError("DialogueManager could not access rootVisualElement."); return false; }

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
        return _panel != null && _npcNameLabel != null && _dialogueText != null
            && _playerInput != null && _closeButton != null && sendButton != null;
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

    private void EnsureSuggestionsRow()
    {
        if (_suggestionsRow != null && _suggestionsRow.parent != null) return;
        if (_panel == null) return;

        _suggestionsRow = _root.Q<VisualElement>("suggestions-row");
        if (_suggestionsRow != null) return;

        _suggestionsRow = new VisualElement { name = "suggestions-row" };
        _suggestionsRow.AddToClassList("suggestions-row");
        _suggestionsRow.style.flexDirection = FlexDirection.Column;
        _suggestionsRow.style.marginTop = 6;
        _suggestionsRow.style.marginBottom = 6;

        VisualElement inputRow = _panel.Q<VisualElement>(className: "input-row");
        if (inputRow != null && inputRow.parent == _panel)
        {
            int idx = _panel.IndexOf(inputRow);
            _panel.Insert(idx, _suggestionsRow);
        }
        else _panel.Add(_suggestionsRow);
    }

    private void ClearSuggestions()
    {
        if (_suggestionsRow == null) return;
        _suggestionsRow.Clear();
        _suggestionsRow.style.display = DisplayStyle.None;
    }

    private void ShowSuggestions(string[] suggestions)
    {
        EnsureSuggestionsRow();
        if (_suggestionsRow == null) return;

        _suggestionsRow.Clear();
        if (suggestions == null || suggestions.Length == 0)
        {
            _suggestionsRow.style.display = DisplayStyle.None;
            return;
        }

        _suggestionsRow.style.display = DisplayStyle.Flex;
        for (int i = 0; i < suggestions.Length; i++)
        {
            string s = suggestions[i];
            if (string.IsNullOrWhiteSpace(s)) continue;
            string trimmed = s.Trim();
            var btn = new Button(() => OnSuggestionChosen(trimmed)) { text = trimmed };
            btn.AddToClassList("suggestion-button");
            btn.style.marginBottom = 4;
            btn.style.whiteSpace = WhiteSpace.Normal;
            btn.style.unityTextAlign = TextAnchor.MiddleLeft;
            _suggestionsRow.Add(btn);
        }
    }

    private void OnSuggestionChosen(string suggestion)
    {
        if (_isWaitingForReply) return;
        if (_playerInput == null) return;
        _playerInput.value = suggestion;
        ClearSuggestions();
        SubmitPlayerInput();
    }

    private void OnPlayerInputKeyDown(KeyDownEvent evt)
    {
        if (evt == null) return;
        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
        SubmitPlayerInput();
        evt.StopPropagation();
        evt.PreventDefault();
    }
}
