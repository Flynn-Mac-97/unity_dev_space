using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using Flynn.Npc.Memory;
using Flynn.Events;


using Flynn.Core;
using Flynn.Resources;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
using Flynn.Player;
using Flynn.Transmitter;
using Flynn.Tutorial;
namespace Flynn.Npc
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }
        public static bool IsDialogueOpen { get; private set; }

        [SerializeField] private UIDocument uiDocument;

        private VisualElement _root;
        private Label _npcNameLabel;
        private Label _dialogueText;
        private VisualElement _npcPortrait;
        private VisualElement _playerPortrait;
        private TextField _playerInput;
        private Button _closeButton;
        private VisualElement _panel;
        private VisualElement _suggestionsRow;

        private PlayerController2D _playerController;
        private Button _codexButton;
        private VisualElement _trustBarFill;
        private Label _trustValueLabel;
        private Label _secretsCounter;
        private Label _trustFloatingText;

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

        // Typewriter + thinking indicator
        private Coroutine _typewriterRoutine;
        private Coroutine _thinkingRoutine;
        private ScrollView _conversationScroll;
        private const int k_MaxHistoryLabels = 20;
        private const float k_TypewriterCharDelay = 0.015f;
        private string _pendingFullReply;
        private string _pendingHighlightedReply;
        private string _pendingReplyText; // For AddTurn after typewriter completes

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("Multiple DialogueManager instances detected. Replacing previous instance.");
            Instance = this;
        }

        private void Start() { TryBindUi(); }

        public void OpenAgent(string npcId, Sprite portraitOverride = null, NpcRelationshipState relationshipState = null, Vector3 npcWorldPosition = default)
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
            Debug.Log($"[Dialogue] OpenAgent: npcId={npcId}, hasRelationship={relationshipState != null}, " +
                $"trust={(_activeRelationship != null ? _activeRelationship.trust : (_activeNpc != null ? _activeNpc.startingTrust : 0))}");
            _isWaitingForReply = false;
            _recentTurns.Clear();
            ClearConversationHistory();
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
            ApplyPlayerPortraitSprite();
            _playerInput.value = string.Empty;
            ClearSuggestions();
            _panel.RemoveFromClassList("hidden");
            IsDialogueOpen = true;
            LockPlayerMovement(true, npcWorldPosition);
            RefreshTrustDisplay();
            RefreshTopicsRow();
            RefreshItemsRow();

            if (GameEventBus.Instance != null)
                GameEventBus.Instance.Publish(new NpcDialogueOpened(npcId));
        }

        public void Close()
        {
            if (!TryBindUi()) return;
            _isWaitingForReply = false;
            StopThinking();
            if (_typewriterRoutine != null) { StopCoroutine(_typewriterRoutine); _typewriterRoutine = null; _pendingFullReply = null; _pendingHighlightedReply = null; _pendingReplyText = null; }
            _panel.AddToClassList("hidden");
            IsDialogueOpen = false;
            LockPlayerMovement(false);
            _playerInput.value = string.Empty;
            ClearSuggestions();
            ClearTopicsRow();
            ClearItemsRow();
            DialogueHighlighter.ClearCache();
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
            if (_typewriterRoutine != null) { StopCoroutine(_typewriterRoutine); _typewriterRoutine = null; _pendingFullReply = null; _pendingHighlightedReply = null; _pendingReplyText = null; }
            _thinkingRoutine = StartCoroutine(ThinkingDotsRoutine());
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
            if (ctx != null)
                ctx.worldState = GatherWorldState();
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
            string[] suggestionVerbs = null;
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
                    suggestionVerbs = envelope.suggested_reply_verbs;
                    _lastTopic = envelope.topic;
                    Debug.Log("[NPC JSON] " + envelope.ToDebugString());
                    yield return StartCoroutine(WriteEnvelopeMemoryUpdates(envelope, sceneLlm));
                    RaiseEnvelopeSignals(envelope, sceneLlm);
                    ApplyRelationshipDeltas(envelope);
                    PlayerCodex.Instance?.OnDialogueTurnCompleted(_activeNpcId, envelope.topic, envelope);
                    RefreshTrustDisplay();
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
            ShowSuggestions(suggestions, suggestionVerbs);
            RefreshTopicsRow(); // trust may have crossed a threshold this turn
            RefreshItemsRow();  // counts may have changed (gift consumed)
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

        // DECISION: Queries gameplay objects directly each turn. Simple for MVP —
        // only a handful of objects on the tutorial island. For larger scenes,
        // switch to a cached WorldStateProvider.
        private static string GatherWorldState()
        {
            var sb = new System.Text.StringBuilder();
            var station = FindFirstObjectByType<TransmitterStation>();
            if (station != null)
            {
                sb.Append("- Transmitter power: ").Append((int)station.Power).Append("/100");
                if (station.IsPowered) sb.Append(" (steady)");
                sb.Append('\n');
            }

            var collectors = FindObjectsByType<SolarCollector>(FindObjectsSortMode.None);
            if (collectors != null && collectors.Length > 0)
            {
                int cleaned = 0;
                for (int i = 0; i < collectors.Length; i++)
                    if (collectors[i].IsCleaned) cleaned++;
                sb.Append("- Solar collectors: ").Append(cleaned).Append('/').Append(collectors.Length).Append(" cleaned");
                if (cleaned == collectors.Length) sb.Append(" (all done)");
                sb.Append('\n');
            }

            var relay = FindFirstObjectByType<SignalRelay>();
            if (relay != null)
            {
                sb.Append("- Signal relay: ").Append(relay.IsActivated ? "activated" : "dormant");
                sb.Append('\n');
            }

            return sb.ToString().TrimEnd();
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
        // Fallback memory summary when semantic recall is unavailable (no embeddings).
        // Reads recent memories from the DB and scores them by keyword relevance.
        private string BuildMemorySummary(SceneLlmManager sceneLlm, ResolvedNpcContext ctx)
        {
            if (string.IsNullOrEmpty(_activeNpcId) || sceneLlm == null || sceneLlm.MemoryDb == null) return "None yet.";

            var facts = new List<MemoryDoc>();
            foreach (var m in sceneLlm.MemoryDb.Memories.Find(x => x.NpcId == _activeNpcId))
                if (!string.IsNullOrWhiteSpace(m.Text)) facts.Add(m);
            if (facts.Count == 0) return "None yet.";

            var tokens = new HashSet<string>();
            if (ctx != null && ctx.resolvedThing != null)
            {
                AddRelevanceTokens(tokens, ctx.resolvedThing.displayName);
                if (ctx.resolvedThing.aliases != null)
                    foreach (var a in ctx.resolvedThing.aliases) AddRelevanceTokens(tokens, a);
            }

            var scored = new List<KeyValuePair<MemoryDoc, int>>();
            for (int i = 0; i < facts.Count; i++)
            {
                string lower = facts[i].Text.ToLowerInvariant();
                int score = 0;
                foreach (var t in tokens) { if (lower.Contains(t)) { score += 2; break; } }
                if (lower.StartsWith("[player]") || lower.StartsWith("[relationship]") || lower.StartsWith("[disclosure]"))
                    score += 1;
                scored.Add(new KeyValuePair<MemoryDoc, int>(facts[i], score));
            }

            LlmPromptConfig settings = GetPromptConfig(sceneLlm);
            int take = Mathf.Min(settings != null ? Mathf.Clamp(settings.memoryFactsLimit, 1, 7) : 5, scored.Count);

            scored.Sort((a, b) => b.Value != a.Value ? b.Value - a.Value : b.Key.CreatedTurn - a.Key.CreatedTurn);
            var chosen = new List<MemoryDoc>();
            for (int i = 0; i < take; i++) chosen.Add(scored[i].Key);
            chosen.Sort((a, b) => a.CreatedTurn.CompareTo(b.CreatedTurn));

            var lines = new List<string> { "Known facts:" };
            foreach (var m in chosen)
            {
                string tag = string.IsNullOrWhiteSpace(m.Subject) || m.Subject == "world"
                    ? "" : "[" + m.Subject + "] ";
                lines.Add("- " + tag + m.Text.Trim());
            }
            return lines.Count > 1 ? string.Join("\n", lines) : "None yet.";
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
            SceneLlmManager.Instance;

        private static LlmPromptConfig GetPromptConfig(SceneLlmManager sceneLlm) => sceneLlm != null ? sceneLlm.promptConfig : null;
        private LlmPromptConfig GetPromptConfig() => GetPromptConfig(ResolveSceneLlm());

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
            if (string.IsNullOrEmpty(_activeNpcId) || sceneLlm == null || sceneLlm.MemoryDb == null) return history;

            int windowSize = GetChatHistoryWindow();

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
            AppendTurnToHistory(speakerName, normalizedContent);

            int cap = GetRecentTurnsCap();
            while (_recentTurns.Count > cap) _recentTurns.RemoveAt(0);

            if (string.IsNullOrEmpty(_activeNpcId)) return;

            var sceneLlm = ResolveSceneLlm();
            if (sceneLlm == null || sceneLlm.MemoryDb == null) return;

            sceneLlm.MemoryDb.InsertChatTurn(new ChatTurnDoc
            {
                NpcId = _activeNpcId,
                Speaker = speakerName,
                Content = normalizedContent,
                TurnIndex = sceneLlm.MemoryDb.NextTurnIndex(_activeNpcId),
            });
        }

        private IEnumerator WriteEnvelopeMemoryUpdates(NpcReplyEnvelope envelope, SceneLlmManager sceneLlm)
        {
            if (envelope == null || envelope.memory_updates == null) yield break;
            if (string.IsNullOrEmpty(_activeNpcId)) yield break;
            if (sceneLlm == null || sceneLlm.MemoryDb == null) yield break;

            LlmPromptConfig settings = GetPromptConfig(sceneLlm);
            int maxFactLength = settings != null ? Mathf.Max(40, settings.maxFactLength) : 160;
            int turnNo = _turnCount + 1;
            float dedupThreshold = sceneLlm.embeddingSettings != null ? sceneLlm.embeddingSettings.dedupThreshold : 0.92f;

            for (int i = 0; i < envelope.memory_updates.Length; i++)
            {
                var update = envelope.memory_updates[i];
                if (update == null || string.IsNullOrWhiteSpace(update.fact)) continue;

                string subject = NormalizeMemorySubject(update.subject);
                string fact = update.fact.Trim();
                if (maxFactLength > 0 && fact.Length > maxFactLength) fact = fact.Substring(0, maxFactLength);

                float[] vec = null;
                if (sceneLlm.Embedder != null)
                {
                    string err = null;
                    yield return StartCoroutine(sceneLlm.Embedder.Embed(fact, (v, e) => { vec = v; err = e; }));
                    if (vec == null) Debug.LogWarning("[Dialogue] Fact embed failed, storing without vector: " + err);
                }

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
                }, dedupThreshold);
            }
        }

        // Validate each id in envelope.triggers_fired against THIS turn's signal allowlist
        // (so the model can't invent or replay non-repeatable signals), then raise survivors.
        private void RaiseEnvelopeSignals(NpcReplyEnvelope envelope, SceneLlmManager sceneLlm)
        {
            if (envelope == null || envelope.triggers_fired == null || envelope.triggers_fired.Length == 0) return;
            if (string.IsNullOrEmpty(_activeNpcId)) return;
            if (sceneLlm == null || sceneLlm.MemoryDb == null) return;

            var hub = sceneLlm.islandContent;
            var db = sceneLlm.MemoryDb;
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

                if (!signal.repeatable && db.WasTriggerFired(_activeNpcId, trimmed))
                {
                    Debug.Log("[Dialogue] Signal '" + trimmed + "' already fired and not repeatable — dropped.");
                    continue;
                }

                Debug.Log("[Dialogue] Signal fired: " + trimmed + " (by " + _activeNpcId + ")");
                channel?.Raise(trimmed, _activeNpcId, signal.handler, signal.payloadJson);
                db.MarkTriggerFired(_activeNpcId, trimmed);
            }
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

        // Applies envelope relationship_deltas to the live NpcRelationshipState.
        // Previously parsed and dropped (confirmed bug in NPC_Memory.md line 230).
        private void ApplyRelationshipDeltas(NpcReplyEnvelope envelope)
        {
            if (envelope?.relationship_deltas == null)
            {
                Debug.Log("[Dialogue] ApplyRelationshipDeltas: envelope.relationship_deltas is null, skipping.");
                return;
            }
            if (_activeRelationship == null)
            {
                Debug.LogWarning("[Dialogue] ApplyRelationshipDeltas: _activeRelationship is null! " +
                    "Ensure the NPC GameObject has a NpcRelationshipState component.");
                return;
            }

            int trust = envelope.relationship_deltas.trust;
            int affection = envelope.relationship_deltas.affection;
            int suspicion = envelope.relationship_deltas.suspicion;
            Debug.Log($"[Dialogue] ApplyRelationshipDeltas: trust={trust} affection={affection} suspicion={suspicion} (current trust={_activeRelationship.trust})");

            if (trust != 0)
            {
                int trustBefore = _activeRelationship.trust;
                _activeRelationship.AdjustTrust(trust);
                int trustAfter = _activeRelationship.trust;
                Debug.Log($"[Dialogue] Trust delta applied: {trust} → now {trustAfter}");

                // Floating text near trust bar
                ShowTrustFloatingText(trust);

                // Audio feedback
                if (trust > 0) CodexAudio.PlayTrustUp();

                // Milestone check — did we cross a secret threshold?
                CheckTrustMilestone(trustBefore, trustAfter);
            }
        }

        // Shows a brief floating "+1" or "-1" near the trust bar.
        private void ShowTrustFloatingText(int delta)
        {
            if (_trustFloatingText == null) return;
            _trustFloatingText.text = (delta > 0 ? "+" : "") + delta.ToString();
            _trustFloatingText.style.display = DisplayStyle.Flex;
            _trustFloatingText.RemoveFromClassList("visible");
            // Force layout recalc then show
            _trustFloatingText.AddToClassList("visible");
            _trustFloatingText.style.opacity = 1f;

            CancelInvoke(nameof(HideTrustFloatingText));
            Invoke(nameof(HideTrustFloatingText), 1.5f);
        }

        private void HideTrustFloatingText()
        {
            if (_trustFloatingText == null) return;
            _trustFloatingText.RemoveFromClassList("visible");
            _trustFloatingText.style.opacity = 0f;
            _trustFloatingText.style.display = DisplayStyle.None;
        }

        // Checks if crossing a trust threshold unlocked a new secret category.
        private void CheckTrustMilestone(int trustBefore, int trustAfter)
        {
            if (string.IsNullOrEmpty(_activeNpcId)) return;
            if (PlayerCodex.Instance == null) return;

            var locked = PlayerCodex.Instance.GetLockedSecrets(_activeNpcId, trustAfter);
            var wasLocked = PlayerCodex.Instance.GetLockedSecrets(_activeNpcId, trustBefore);

            // If something was locked before but is now unlocked, a threshold was crossed.
            if (wasLocked.Count > locked.Count)
            {
                int unlocked = wasLocked.Count - locked.Count;
                Debug.Log($"[Dialogue] Trust milestone! {unlocked} secret(s) unlocked at trust {trustAfter}");

                // Pulse the trust bar
                if (_trustBarFill != null)
                {
                    _trustBarFill.AddToClassList("pulse");
                    CancelInvoke(nameof(StopTrustPulse));
                    Invoke(nameof(StopTrustPulse), 0.6f);
                }

                // Play secret unlock sound
                CodexAudio.PlaySecretUnlock();

                // Show milestone toast via the codex panel
                var codexPanel = FindFirstObjectByType<Flynn.UI.Screens.Codex.CodexPanelController>();
                codexPanel?.ShowToast($"✦ Secret Unlocked — The Station is ready to share more");
            }
        }

        private void StopTrustPulse()
        {
            if (_trustBarFill != null) _trustBarFill.RemoveFromClassList("pulse");
        }

        // Public method for the codex panel to pre-fill the input when clicking an entry.
        public void SetPlayerInput(string text)
        {
            if (_playerInput == null) return;
            _playerInput.value = text;
            _playerInput.Focus();
        }

        // Updates the trust dots + value + secrets counter in the dialogue footer.
        public void RefreshTrustDisplay()
        {
            int trust = GetCurrentTrust();
            Debug.Log($"[Dialogue] RefreshTrustDisplay: trust={trust}, hasBar={_trustBarFill != null}, hasValue={_trustValueLabel != null}, hasSecrets={_secretsCounter != null}");

            if (_trustValueLabel != null)
                _trustValueLabel.text = trust.ToString();

            if (_trustBarFill != null)
            {
                _trustBarFill.style.width = new Length(trust, LengthUnit.Percent);
            }

            if (_secretsCounter != null && !string.IsNullOrEmpty(_activeNpcId))
            {
                var codex = PlayerCodex.Instance;
                if (codex != null)
                {
                    int total = codex.GetTotalSecretCount(_activeNpcId);
                    int revealed = codex.GetRevealedSecretCount(_activeNpcId);
                    _secretsCounter.text = $"Secrets {revealed}/{total}";
                }
            }
        }

        // Toggles the codex panel open/closed. Called from the codex button.
        private void ToggleCodex()
        {
            // The CodexPanelController handles its own visibility. We find it
            // via the UI system rather than holding a direct reference.
            var codexPanel = FindFirstObjectByType<Flynn.UI.Screens.Codex.CodexPanelController>();
            if (codexPanel != null)
                codexPanel.Toggle();
        }

        private void ApplyNpcReply(string reply)
        {
            StopThinking();
            // DECISION: Don't call AddTurn here — it adds the reply to history
            // immediately, which makes the typewriter pointless (user sees the
            // full text in history before it finishes typing). AddTurn is called
            // at the end of TypewriterRoutine instead.
            if (_typewriterRoutine != null) StopCoroutine(_typewriterRoutine);

            // Pre-compute the highlighted version (applied after typewriter completes).
            var sceneLlm = ResolveSceneLlm();
            var hub = sceneLlm != null ? sceneLlm.islandContent : null;
            string highlighted = DialogueHighlighter.Highlight(reply, _lastTopic, hub);
            Debug.Log($"[Dialogue] ApplyNpcReply: replyLen={reply.Length}, highlightedLen={highlighted.Length}, " +
                $"changed={reply != highlighted}, topic={_lastTopic ?? "(null)"}");
            _pendingHighlightedReply = highlighted;
            _pendingReplyText = reply; // Stored for AddTurn after typewriter completes

            Debug.Log("[Dialogue] Starting typewriter routine...");
            _typewriterRoutine = StartCoroutine(TypewriterRoutine(reply));
            if (_playerInput != null) _playerInput.Focus();
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            Debug.Log($"[Dialogue] Typewriter START: {fullText.Length} chars, delay={k_TypewriterCharDelay}s");
            _pendingFullReply = fullText;
            _dialogueText.text = "";
            int tickCounter = 0;
            for (int i = 1; i <= fullText.Length; i++)
            {
                char c = fullText[i - 1];
                _dialogueText.text = fullText.Substring(0, i);
                // Play typewriter tick on visible characters only, every 3rd char
                // to avoid audio spam on short replies.
                if (!char.IsWhiteSpace(c) && tickCounter % 3 == 0)
                    CodexAudio.PlayTypewriterTick();
                tickCounter++;
                yield return new WaitForSecondsRealtime(k_TypewriterCharDelay);
            }
            // Swap to highlighted version (rich text with <color> tags on known terms).
            Debug.Log($"[Dialogue] Typewriter DONE. Swapping to highlighted version (has highlight={_pendingHighlightedReply != null && _pendingHighlightedReply != fullText})");
            _dialogueText.text = _pendingHighlightedReply ?? fullText;

            // Now that the typewriter is done, add the reply to history + DB.
            // This was delayed so the user sees the typewriter effect, not the
            // full text appearing instantly in the history scroll.
            if (!string.IsNullOrEmpty(_pendingReplyText))
            {
                string npcName = _npcNameLabel != null ? _npcNameLabel.text : "NPC";
                AddTurn(npcName, _pendingReplyText);
                _pendingReplyText = null;
            }

            _pendingFullReply = null;
            _pendingHighlightedReply = null;
            _typewriterRoutine = null;
        }

        // Handles clicks on the dialogue text: skip typewriter if still typing,
        // or check if a highlighted term was clicked and pre-fill the input.
        private void OnDialogueTextClick(ClickEvent evt)
        {
            // If typewriter is running, click skips it.
            if (_typewriterRoutine != null)
            {
                SkipTypewriter();
                return;
            }

            // If waiting for LLM reply, ignore clicks.
            if (_isWaitingForReply) return;

            // Check if a highlighted term was clicked.
            if (_dialogueText == null) return;
            string term = DialogueHighlighter.GetTermAtClick(evt.localPosition, _dialogueText);
            if (!string.IsNullOrEmpty(term))
            {
                SetPlayerInput($"Tell me more about {term}");
                Debug.Log($"[Dialogue] Clicked term: {term}");
            }
        }

        private void SkipTypewriter()
        {
            if (_typewriterRoutine == null || _pendingFullReply == null) return;
            StopCoroutine(_typewriterRoutine);            _dialogueText.text = _pendingHighlightedReply ?? _pendingFullReply;

            // Add to history on skip too
            if (!string.IsNullOrEmpty(_pendingReplyText))
            {
                string npcName = _npcNameLabel != null ? _npcNameLabel.text : "NPC";
                AddTurn(npcName, _pendingReplyText);
                _pendingReplyText = null;
            }

            _pendingFullReply = null;
            _pendingHighlightedReply = null;
            _typewriterRoutine = null;
        }

        private IEnumerator ThinkingDotsRoutine()
        {
            Debug.Log("[Dialogue] ThinkingDots START");
            string[] frames = { "Thinking.", "Thinking..", "Thinking..." };
            int i = 0;
            while (true)
            {
                _dialogueText.text = frames[i];
                i = (i + 1) % frames.Length;
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }

        private void StopThinking()
        {
            if (_thinkingRoutine != null) { StopCoroutine(_thinkingRoutine); _thinkingRoutine = null; Debug.Log("[Dialogue] ThinkingDots STOPPED"); }
        }

        private void AppendTurnToHistory(string speaker, string text)
        {
            if (_conversationScroll == null) return;
            var label = new Label($"{speaker}: {text}");
            label.AddToClassList("history-line");
            label.AddToClassList(speaker == k_PlayerSpeaker ? "history-player" : "history-npc");
            _conversationScroll.Add(label);
            while (_conversationScroll.childCount > k_MaxHistoryLabels)
                _conversationScroll.RemoveAt(0);
            _conversationScroll.ScrollTo(label);
        }

        private void ClearConversationHistory()
        {
            if (_conversationScroll != null) _conversationScroll.Clear();
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

        private void LockPlayerMovement(bool locked, Vector3 faceTarget = default)
        {
            if (_playerController == null)
                _playerController = FindFirstObjectByType<PlayerController2D>();
            if (_playerController != null)
            {
                _playerController.IsMovementLocked = locked;
                if (locked && faceTarget != default)
                    _playerController.FacePoint(faceTarget);
            }
        }

        private void ApplyPlayerPortraitSprite()
        {
            if (_playerPortrait == null) return;

            var sceneLlm = ResolveSceneLlm();
            Sprite playerSprite = sceneLlm != null && sceneLlm.playerProfile != null
                ? sceneLlm.playerProfile.portraitSprite
                : null;

            if (playerSprite == null)
            {
                _playerPortrait.style.backgroundImage = new StyleBackground((Texture2D)null);
                _playerPortrait.AddToClassList("hidden");
                return;
            }

            _playerPortrait.style.backgroundImage = new StyleBackground(playerSprite);
            _playerPortrait.RemoveFromClassList("hidden");
        }

        private void LoadPersistentMemory()
        {
            _recentTurns.Clear();
            if (string.IsNullOrEmpty(_activeNpcId)) return;

            var sceneLlm = ResolveSceneLlm();
            if (sceneLlm == null || sceneLlm.MemoryDb == null) return;

            int cap = GetRecentTurnsCap();
            var turns = new List<ChatTurnDoc>();
            foreach (var t in sceneLlm.MemoryDb.ChatTurns.Find(x => x.NpcId == _activeNpcId))
                turns.Add(t);
            turns.Sort((a, b) => a.TurnIndex.CompareTo(b.TurnIndex));

            int start = Mathf.Max(0, turns.Count - cap);
            for (int i = start; i < turns.Count; i++)
                _recentTurns.Add($"{turns[i].Speaker}: {turns[i].Content}");
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
                _dialogueText.text = "Memory is auto-saved to the DB.";
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
                bool removed = sceneLlm != null && sceneLlm.MemoryDb != null;
                if (removed) sceneLlm.MemoryDb.ClearNpc(_activeNpcId);
                _recentTurns.Clear();
                _lastNpcReply = null;
                _lastTopic = null;
                _dialogueText.text = removed
                    ? "Memory cleared for this NPC."
                    : "No memory DB available.";
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
            if (_dialogueText != null) _dialogueText.enableRichText = true;
            _npcPortrait  = _root.Q<VisualElement>("npc-portrait");
            _playerPortrait = _root.Q<VisualElement>("player-portrait");
            _playerInput  = _root.Q<TextField>("player-input");
            _closeButton  = _root.Q<Button>("close-button");
            _conversationScroll = _root.Q<ScrollView>("conversation-scroll");
            Button sendButton = _root.Q<Button>("send-button");
            _codexButton     = _root.Q<Button>("codex-button");
            _trustBarFill    = _root.Q<VisualElement>("trust-bar-fill");
            _trustValueLabel = _root.Q<Label>("trust-value");
            _secretsCounter   = _root.Q<Label>("secrets-counter");
            _trustFloatingText = _root.Q<Label>("trust-floating-text");

            if (!HasAllRequiredElements(sendButton))
            {
                BuildFallbackUi();
                _panel        = _root.Q<VisualElement>("dialogue-panel");
                _npcNameLabel = _root.Q<Label>("npc-name");
                _dialogueText = _root.Q<Label>("dialogue-text");
            if (_dialogueText != null) _dialogueText.enableRichText = true;
                _npcPortrait  = _root.Q<VisualElement>("npc-portrait");
                _playerPortrait = _root.Q<VisualElement>("player-portrait");
                _playerInput  = _root.Q<TextField>("player-input");
                _closeButton  = _root.Q<Button>("close-button");
                _conversationScroll = _root.Q<ScrollView>("conversation-scroll");
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
                _dialogueText.RegisterCallback<ClickEvent>(OnDialogueTextClick);
                _root.RegisterCallback<KeyDownEvent>(OnPanelKeyDown);
                if (_codexButton != null)
                    _codexButton.RegisterCallback<ClickEvent>(_ => ToggleCodex());
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

            VisualElement playerPortrait = new VisualElement { name = "player-portrait" };
            playerPortrait.AddToClassList("player-portrait");
            playerPortrait.AddToClassList("hidden");

            VisualElement npcPortrait = new VisualElement { name = "npc-portrait" };
            npcPortrait.AddToClassList("npc-portrait");
            npcPortrait.AddToClassList("hidden");

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

            contentRow.Add(playerPortrait);
            contentRow.Add(dialogueText);
            contentRow.Add(npcPortrait);

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

        // ── Topics row: codex asks + locked-knowledge teases ─────────────

        private VisualElement _topicsRow;

        private void EnsureTopicsRow()
        {
            if (_topicsRow != null || _panel == null) return;

            // Preferred: the predefined container inside the context strip (UXML).
            _topicsRow = _panel.Q<VisualElement>("topics-row");
            if (_topicsRow != null) return;

            // Fallback UI path: build one next to the input row.
            _topicsRow = new VisualElement();
            _topicsRow.AddToClassList("topics-row");
            _topicsRow.style.flexDirection = FlexDirection.Row;
            _topicsRow.style.flexWrap = Wrap.Wrap;
            _topicsRow.style.display = DisplayStyle.None;

            VisualElement inputRow = _panel.Q<VisualElement>(className: "input-row");
            if (inputRow != null && inputRow.parent != null)
                inputRow.parent.Insert(inputRow.parent.IndexOf(inputRow), _topicsRow);
            else _panel.Add(_topicsRow);
        }

        private void ClearTopicsRow()
        {
            if (_topicsRow == null) return;
            _topicsRow.Clear();
            _topicsRow.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Rebuilds the topic strip: up to 3 "Ask: topic" chips from the player's
        /// codex, plus up to 3 locked-knowledge teases ("[locked] secret — trust 60")
        /// that show WHAT kind of thing is gated without leaking its text. Locked
        /// chips submit a gentle probe line. Refreshed per turn — crossing a trust
        /// threshold visibly unlocks topics mid-conversation.
        /// </summary>
        private void RefreshTopicsRow()
        {
            EnsureTopicsRow();
            if (_topicsRow == null) return;
            _topicsRow.Clear();

            var sceneLlm = ResolveSceneLlm();
            var db = sceneLlm != null ? sceneLlm.MemoryDb : null;
            if (db == null || !db.IsOpen || string.IsNullOrEmpty(_activeNpcId))
            {
                _topicsRow.style.display = DisplayStyle.None;
                return;
            }

            int trust = GetCurrentTrust();

            // Codex topics → direct asks (most recent first, distinct, max 3)
            var seen = new HashSet<string>();
            var entries = db.GetAllCodexEntries();
            int asks = 0;
            for (int i = entries.Count - 1; i >= 0 && asks < 3; i--)
            {
                string topic = entries[i] != null ? entries[i].Topic : null;
                if (string.IsNullOrWhiteSpace(topic)) continue;
                topic = topic.Trim();
                if (!seen.Add(topic.ToLowerInvariant())) continue;

                string ask = "Tell me about " + topic + ".";
                var btn = new Button(() => OnSuggestionChosen(ask)) { text = "Ask: " + topic };
                StyleTopicChip(btn, locked: false);
                _topicsRow.Add(btn);
                asks++;
            }

            // Locked knowledge teases — nearest thresholds first, never Avoid entries
            var locked = new List<KnowledgeDoc>();
            foreach (var k in db.GetKnowledgeMeta(_activeNpcId))
            {
                if (k.RevealTrust <= 0 || k.RevealTrust <= trust) continue;
                if (!string.IsNullOrEmpty(k.Kind) && k.Kind.Trim().ToLowerInvariant() == "avoid") continue;
                locked.Add(k);
            }
            locked.Sort((a, b) => a.RevealTrust.CompareTo(b.RevealTrust));
            for (int i = 0; i < locked.Count && i < 3; i++)
            {
                var k = locked[i];
                string kind = string.IsNullOrWhiteSpace(k.Kind) ? "secret" : k.Kind.Trim().ToLowerInvariant();
                var btn = new Button(() => OnSuggestionChosen("I get the feeling you're holding something back."))
                {
                    text = $"[locked] {kind} — trust {k.RevealTrust}"
                };
                StyleTopicChip(btn, locked: true);
                _topicsRow.Add(btn);
            }

            _topicsRow.style.display = _topicsRow.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void StyleTopicChip(Button btn, bool locked)
        {
            btn.AddToClassList("suggestion-button");
            btn.AddToClassList(locked ? "topic-chip--locked" : "topic-chip");
            btn.style.marginRight = 4;
            btn.style.marginBottom = 4;
            btn.style.whiteSpace = WhiteSpace.Normal;
            btn.style.borderLeftWidth = 3;
            if (locked)
            {
                btn.style.opacity = 0.55f;
                btn.style.borderLeftColor = new Color(1f, 1f, 1f, 0.25f);
            }
            else
            {
                btn.style.borderLeftColor = new Color(1f, 1f, 1f, 0.6f);
            }
        }

        // ── Items row: show / gift inventory items in conversation ───────

        private VisualElement _itemsRow;

        private void EnsureItemsRow()
        {
            if (_itemsRow != null || _panel == null) return;

            // Preferred: the predefined container inside the context strip (UXML).
            _itemsRow = _panel.Q<VisualElement>("items-row");
            if (_itemsRow != null) return;

            // Fallback UI path: build one next to the input row.
            _itemsRow = new VisualElement();
            _itemsRow.AddToClassList("items-row");
            _itemsRow.style.flexDirection = FlexDirection.Row;
            _itemsRow.style.flexWrap = Wrap.Wrap;
            _itemsRow.style.display = DisplayStyle.None;

            VisualElement inputRow = _panel.Q<VisualElement>(className: "input-row");
            if (inputRow != null && inputRow.parent != null)
                inputRow.parent.Insert(inputRow.parent.IndexOf(inputRow), _itemsRow);
            else _panel.Add(_itemsRow);
        }

        private void ClearItemsRow()
        {
            if (_itemsRow == null) return;
            _itemsRow.Clear();
            _itemsRow.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// One chip per held item: click = SHOW (item stays), small "gift" button =
        /// GIVE (removes one from the stack first, then tells the NPC). The wrench
        /// (slot 0) can be shown but never gifted — it's the player's core tool.
        /// </summary>
        private void RefreshItemsRow()
        {
            EnsureItemsRow();
            if (_itemsRow == null) return;
            _itemsRow.Clear();

            var inv = Flynn.Player.PlayerInventory.Instance;
            if (inv == null)
            {
                _itemsRow.style.display = DisplayStyle.None;
                return;
            }

            for (int i = 0; i < inv.SlotCount; i++)
            {
                var slot = inv.GetSlot(i);
                if (slot.IsEmpty) continue;

                string name = !string.IsNullOrWhiteSpace(slot.item.displayName) ? slot.item.displayName : slot.item.name;
                string countSuffix = slot.count > 1 ? $" ×{slot.count}" : "";

                var showBtn = new Button(() => OnSuggestionChosen($"(I hold out my {name} to show you.) What do you make of it?"))
                {
                    text = $"[◆] {name}{countSuffix}"
                };
                StyleItemChip(showBtn, isGift: false);
                if (slot.item.icon != null)
                {
                    var img = new Image { sprite = slot.item.icon, scaleMode = ScaleMode.ScaleToFit };
                    img.style.width = 14; img.style.height = 14; img.style.marginRight = 3;
                    showBtn.Insert(0, img);
                }
                _itemsRow.Add(showBtn);

                if (i != Flynn.Player.PlayerInventory.WrenchSlot)
                {
                    int slotIndex = i; // capture per-iteration
                    string giftName = name;
                    var giftBtn = new Button(() => OnGiftClicked(slotIndex, giftName)) { text = "gift" };
                    StyleItemChip(giftBtn, isGift: true);
                    _itemsRow.Add(giftBtn);
                }
            }

            _itemsRow.style.display = _itemsRow.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnGiftClicked(int slotIndex, string itemName)
        {
            // Check the gate BEFORE consuming — never eat an item without sending the line.
            if (_isWaitingForReply) return;
            var inv = Flynn.Player.PlayerInventory.Instance;
            if (inv == null) return;

            var removed = inv.RemoveFromSlot(slotIndex, 1, out int removedCount);
            if (removed == null || removedCount <= 0) return;

            RefreshItemsRow();
            OnSuggestionChosen($"(I hand you my {itemName} — a gift.)");
        }

        private static void StyleItemChip(Button btn, bool isGift)
        {
            btn.AddToClassList("suggestion-button");
            btn.AddToClassList(isGift ? "item-chip--gift" : "item-chip");
            btn.style.marginRight = isGift ? 8 : 2;
            btn.style.marginBottom = 4;
            btn.style.borderLeftWidth = 3;
            btn.style.borderLeftColor = isGift
                ? new Color(0.85f, 0.75f, 0.35f)          // gift — gold (matches show verb)
                : new Color(0.85f, 0.75f, 0.35f, 0.55f);  // show — dim gold
            if (isGift) btn.style.opacity = 0.85f;
        }

        private void ShowSuggestions(string[] suggestions, string[] verbs = null)
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

                string verb = NormalizeChipVerb(verbs != null && i < verbs.Length ? verbs[i] : null);
                var btn = new Button(() => OnSuggestionChosen(trimmed))
                {
                    text = ChipVerbGlyph(verb) + trimmed,
                    userData = trimmed // clean text — number-key path submits this, not the display text
                };
                btn.AddToClassList("suggestion-button");
                btn.AddToClassList("suggestion-button--" + verb);
                btn.style.marginBottom = 4;
                btn.style.whiteSpace = WhiteSpace.Normal;
                btn.style.unityTextAlign = TextAnchor.MiddleLeft;

                // Verb accent: left edge stripe (B/W-friendly, readable on the flat black panel).
                btn.style.borderLeftWidth = 3;
                btn.style.borderLeftColor = ChipVerbColor(verb);

                _suggestionsRow.Add(btn);
            }
        }

        /// <summary>ask = neutral, press = risky push (may cost suspicion, may force secrets),
        /// joke = affection play, show = item presentation.</summary>
        private static string NormalizeChipVerb(string raw)
        {
            switch ((raw ?? "").Trim().ToLowerInvariant())
            {
                case "press": return "press";
                case "joke": return "joke";
                case "show": return "show";
                default: return "ask";
            }
        }

        private static string ChipVerbGlyph(string verb)
        {
            switch (verb)
            {
                case "press": return "[!] ";
                case "joke": return "[~] ";
                case "show": return "[◆] ";
                default: return "";
            }
        }

        private static Color ChipVerbColor(string verb)
        {
            switch (verb)
            {
                case "press": return new Color(0.85f, 0.45f, 0.35f); // pushing — warm warning
                case "joke": return new Color(0.35f, 0.75f, 0.65f);  // playful — teal
                case "show": return new Color(0.85f, 0.75f, 0.35f);  // item — gold
                default: return new Color(1f, 1f, 1f, 0.35f);        // ask — quiet white
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

        private void OnPanelKeyDown(KeyDownEvent evt)
        {
            if (evt == null) return;

            if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
                return;
            }

            // Skip typewriter on any key when typing
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                SkipTypewriter();

            // Number keys 1-4 select suggestion buttons
            if (evt.keyCode >= KeyCode.Alpha1 && evt.keyCode <= KeyCode.Alpha4)
            {
                int idx = evt.keyCode - KeyCode.Alpha1;
                if (_suggestionsRow != null && idx < _suggestionsRow.childCount)
                {
                    var btn = _suggestionsRow[idx] as Button;
                    if (btn != null)
                    {
                        // userData holds the clean reply text (btn.text carries the verb glyph)
                        OnSuggestionChosen(btn.userData as string ?? btn.text);
                        evt.StopPropagation();
                    }
                }
            }
        }
    }

}
