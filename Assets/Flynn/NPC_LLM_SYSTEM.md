# LLM NPC Dialogue System — Onboarding

Read this before touching anything under `Assets/Flynn/Scripts/NPC/`. Companion files: `Assets/Flynn/CLAUDE.md` (Flynn-wide rules), `Assets/Flynn/llm_training_log.md` (tuning record).

Last updated: 2026-05-31.

---

## 1. What this system is

A conversational NPC system backed by an LLM. The player talks to an NPC in free text; the model replies **in character** as a structured JSON envelope — **one call per turn**, no post-processing pipeline. Designer-authored ScriptableObjects define each NPC's persona, knowledge, triggers, and trust thresholds. Scene-side `NpcContextNode` components inject world prose so the NPC can speak accurately about what's around them.

Test character: **Maren_WindKeeper** — a Ghibli-warm island wind-keeper.

---

## 2. Providers

Switched scene-wide via `SceneLlmManager.provider`:

### Local (Ollama)
- `npc-qwen35-2b` (Qwen3.5 2B, Q4_K_M). ChatML `TEMPLATE` required in Modelfile.
- Short, positive-only system prompt. `keep_alive 30m`, `repeat_penalty 1.15`, `num_ctx 8192`.
- Files at `C:\Users\DriveSIM\models\qwen35\2b\` (outside repo).
- Settings SO: `LocalModelSettings`.

### OpenRouter
- OpenAI-compatible route. Current model: `openai/gpt-4o-mini`.
- Settings SO: `RemoteModelSettings` with `forceJsonMode` + `requireJsonProvider`.
- API key in EditorPrefs via **Flynn → NPC → OpenRouter Settings**; falls back to env var `OPENROUTER_API_KEY` in builds (`OpenRouterApiKey.Resolve`).

---

## 3. Data model

### `NpcData` — the only per-NPC SO ([NpcData.cs](Scripts/NPC/Runtime/Data/NpcData.cs))

Everything for one NPC lives on a single asset:
- **Identity** — `npcId` (stable key for memory lookup), `displayName`, `portraitSprite`.
- **Persona** — `role`, `speakingStyle`, `personalityTraits`, `doRules`, `dontRules`, `capabilities`.
- **Knowledge** — `List<KnowledgeEntry>`; each has `KnowledgeKind { Fact, Belief, Rumor, Secret, Avoid }`, `text`, `revealTrustThreshold`.
- **Triggers** — `List<TriggerEntry>`; each has `key`, `TriggerKind { StoryBeat, ClueReveal, OneTime, Repeatable, Forbidden }`, `description`, `draft` flag.
- **Relationship** — `startingTrust`, `trustToShareSecrets`.
- **LLM** — `llmEnabled` (gates all LLM calls), `fallbackReply`, `promptTemplate` (token-substituted by `NpcPromptTokens`).

### Shared SOs
- [LlmPromptConfig.cs](Scripts/NPC/Runtime/Data/LlmPromptConfig.cs) — global `systemPrompt`, `jsonOutputAddendum` (defaults to `NpcReplyEnvelope.PromptAddendum`), memory budget (`recentTurnsLimit`, `memoryFactsLimit`, `maxFactLength`).
- `LocalModelSettings`, `RemoteModelSettings` — endpoint / model / sampling / timeout.
- `PlayerDialogueProfile` — who the player is; used so NPCs write player suggestion-chips in the right voice.

---

## 4. Scene wiring — `SceneLlmManager`

Single scene singleton ([SceneLlmManager.cs](Scripts/NPC/Runtime/Llm/SceneLlmManager.cs)) holding every shared reference:

| Field | Purpose |
|---|---|
| `provider` | `Local` or `OpenRouter` — picks endpoint for the whole scene |
| `sharedLocalModelSettings` / `sharedRemoteModelSettings` | active model config |
| `llmEnabled` | global kill switch |
| `promptConfig` | the `LlmPromptConfig` SO |
| `contextRegistry` | `NpcContextRegistry` SO — scene-info lookup |
| `triggerChannel` | `DialogueTriggerChannel` SO event bus |
| `memoryStore` | `NpcMemoryStore` SO for the active save slot |
| `saveSlotId` | informational label |
| `playerProfile` | `PlayerDialogueProfile` SO |

`GetActiveDialogueConfig()` returns a unified `LlmRequestConfig` (with auth/referer/json-mode headers populated when on OpenRouter).

---

## 5. Runtime flow ([DialogueManager.cs](Scripts/NPC/Runtime/DialogueManager.cs))

1. **`OpenAgent(NpcData)`** — binds UIToolkit panel, loads persisted memory, pauses time (`Time.timeScale = 0`).
2. **`SubmitPlayerInput`** — handles `/save`, `/clear`, `/help` chat commands; otherwise launches `HandleAgentTurn` coroutine if `llmEnabled`.
3. **`BuildSystemPrompt`** assembles, in order:
   - `LlmPromptConfig.systemPrompt`
   - per-NPC `promptTemplate` after `NpcPromptTokens.Apply(template, NpcData)`
   - **Scene context block** — `NpcContextRegistry.GetContextsFor(npc)` returns every linked `NpcContextNode`; their `contextLabel` + `contextDescription` are appended.
   - **Memory summary** — long-term facts from `NpcMemoryStore` (budget = `memoryFactsLimit`).
   - **Player line** — `PlayerDialogueProfile.displayName` + `persona`.
   - **JSON output contract** — `LlmPromptConfig.jsonOutputAddendum` (always last so it dominates).
4. **`BuildChatHistory`** — pulls last N turns from `NpcMemoryStore`, splits speaker prefix, emits alternating user/assistant messages.
5. **`LocalLlmClient.GenerateReply`** — single POST to whichever endpoint `GetActiveDialogueConfig()` returns. URL-routes between Ollama `/api/chat` and OpenAI-compat `/v1/chat/completions`. Adds `Authorization`, `HTTP-Referer`, `X-Title` headers when present.
6. **`NpcReplyEnvelope.TryParse`** — tolerates ```` ```json ```` fences and stray prose by extracting the outer `{...}` via brace counting. Falls back to raw text on parse failure.
7. **`WriteEnvelopeMemoryUpdates`** — persists each `memory_updates[]` entry into `NpcMemoryStore` as `[subject] fact`.
8. **`RaiseEnvelopeTriggers`** — validates each `triggers_fired` key against `NpcData.triggers` (drops invented keys and `draft` entries), then raises survivors on `DialogueTriggerChannel`.
9. **`LlmDebugBus.BeginTurn / RecordStage / EndTurn`** — feeds the debug window.
10. UI shows `reply` as the spoken line and `suggested_player_replies` as clickable chips above the input row.

---

## 6. JSON envelope ([NpcReplyEnvelope.cs](Scripts/NPC/Runtime/Llm/NpcReplyEnvelope.cs))

```json
{
  "reply": "the spoken line, 1-3 short sentences",
  "topic": "short noun phrase",
  "intent": "short verb phrase",
  "tone": "one adjective",
  "mood_shift": "calmer | tenser | same",
  "relationship_deltas": { "trust": 0, "affection": 0, "suspicion": 0 },
  "flags": ["short_tag"],
  "suggested_player_replies": ["three", "terse", "options"],
  "memory_updates": [{ "subject": "player|self|world|relationship|disclosure", "fact": "..." }],
  "triggers_fired": ["trigger_key"]
}
```

- `relationship_deltas` are integers in -2..+2.
- `suggested_player_replies` is exactly three short options.
- `memory_updates.subject` is one of five: **player / self / world / relationship / disclosure**. `NormalizeMemorySubject` accepts a few aliases (`npc`→`self`, `user`→`player`, `trust`→`relationship`, etc.); unknowns fall back to `world`.
- `triggers_fired` keys must match an `NpcData.triggers[i].key` exactly. Invented keys are dropped with a warning.

---

## 7. Scene-side world context

- [NpcContextNode.cs](Scripts/NPC/Runtime/World/NpcContextNode.cs) — drop on any scene object (landmark, prop, NPC, item). Holds `contextLabel`, `contextDescription`, `linkedNpcs[]`. Self-registers with the shared `NpcContextRegistry` on enable.
- [NpcContextRegistry.cs](Scripts/NPC/Runtime/World/NpcContextRegistry.cs) — SO lookup keyed by NPC. `DialogueManager.BuildSceneContext` reads it during prompt assembly.
- Also present: `WorldLandmark`, `LandmarkAnchor`, `LandmarkRegistry` (positional world references).

---

## 8. NPC interaction layer

- [NpcInteraction.cs](Scripts/NPC/Runtime/NpcInteraction.cs) — proximity-gated radial menu on each NPC GameObject. Hotkey **E** invokes the default (first available) action.
- [NpcAction.cs](Scripts/NPC/Runtime/Interaction/NpcAction.cs) — SO base with `actionLabel`, `hotkeyHint`, `IsAvailable`, `Execute`.
- Concrete actions: `TalkAction` (opens dialogue), `LogAction` (debug).
- `NpcRadialMenuBuilder` builds the in-world menu UI.
- `NpcDialogueAuthoringLink` wires a scene `NpcInteraction` MonoBehaviour → `NpcData` SO.

---

## 9. Memory persistence

[NpcMemoryStore.cs](Scripts/NPC/Runtime/Memory/NpcMemoryStore.cs) — **one inspectable SO per save slot**. Contains a list of `Entry { npcId, recentTurns, memoryFacts, firedTriggers }`. Editor-inspectable while a conversation runs (`SetDirty` per mutation); explicit `Save()` flushes to disk. Player builds are runtime-only (no JSON sidecar yet — would need to be added).

Chat commands inside dialogue: `/save`, `/clear`, `/help`.

---

## 10. Debug / UI

- [LlmDebugBus.cs](Scripts/NPC/Runtime/Debug/LlmDebugBus.cs) + [LlmDebugWindowController.cs](UI/Screens/LlmDebugWindow/LlmDebugWindowController.cs) — per-turn system prompt, chat history, raw response, parsed summary, elapsed ms.
- [NpcInfoHudController.cs](UI/Screens/NpcInfoHud/NpcInfoHudController.cs) — in-world NPC info HUD.
- Dialogue panel: shared UXML in the project; `DialogueManager.BuildFallbackUi` constructs a minimal version programmatically if required elements aren't found.

---

## 11. Editor tooling

- **NPC Crafting Studio** (`Tools/Dialogue/NPC Crafting Studio`) — UIToolkit shell, **two tabs**: Profile and Prompt. Styles from `Assets/Flynn/UI/Styles/tokens.uss` + studio-local USS.
- **DemoNpcBuilder** — generates the Maren_WindKeeper asset.
- **OpenRouter Settings window** (`Flynn → NPC → OpenRouter Settings`) — EditorPrefs key storage + model browser.
- Editors: `SceneLlmManagerEditor`, `NpcMemoryStoreMigration`.

---

## 12. Configs (`Assets/Flynn/Configs/NPC/`)

| Asset | Purpose |
|---|---|
| `Maren_WindKeeper.asset` | test NPC (`NpcData`) |
| `LlmPromptConfig.asset` | global prompt + memory budget |
| `NpcContextRegistry.asset` | scene-info lookup |
| `DialogueTriggerChannel.asset` | trigger event bus |
| `NpcMemoryStore_slot_0.asset` | save-slot memory |
| `Actions/` | reusable `NpcAction` assets |

`PlayerDialogueProfile` lives at `Assets/Flynn/Configs/Player/Player_Flynn.asset`. `LocalModelSettings` is at `Assets/ScriptableObjects/Dialogue/Demo/Demo_LocalModelSettings.asset` (outside Flynn). `RemoteModelSettings` at `Assets/Flynn/OpenRouter/Default.asset`.

---

## 13. Key gotchas

- `NpcData.llmEnabled = false` forces the static `fallbackReply` and skips the LLM entirely.
- `SceneLlmManager.provider` is scene-wide, not per-NPC.
- `NpcReplyEnvelope.TryParse` is tolerant but JSON-only — when the model emits prose without a `{...}` block, the raw text is shown and a warning logged.
- `triggers_fired` keys are validated against the active NPC's allowlist (excluding `draft`). Invented keys are dropped silently to the player but logged as warnings.
- Always `EditorUtility.SetDirty(target)` when mutating SOs from Editor scripts.
- Never store scene-instance references inside ScriptableObjects (registries hold runtime-only lookups, cleared on enable/disable).
- Everything stays under `Assets/Flynn/`. Unity 2022.3.62f3 LTS.

---

## 14. File map

```
Assets/Flynn/
  NPC_LLM_SYSTEM.md                                ← this file
  llm_training_log.md                              ← tuning record
  CLAUDE.md                                        ← Flynn-wide rules
  OpenRouter/Default.asset                         ← RemoteModelSettings
  Configs/NPC/
    Maren_WindKeeper.asset                         ← NpcData (test NPC)
    LlmPromptConfig.asset
    NpcContextRegistry.asset
    DialogueTriggerChannel.asset
    NpcMemoryStore_slot_0.asset
    Actions/                                       ← NpcAction assets
  Configs/Player/Player_Flynn.asset                ← PlayerDialogueProfile
  Scripts/NPC/Runtime/
    DialogueManager.cs                             ← orchestrator
    NpcInteraction.cs                              ← proximity + radial menu
    NpcRelationshipState.cs                        ← live trust/affection/suspicion
    NpcRadialMenuBuilder.cs
    Data/
      NpcData.cs                                   ← per-NPC SO (everything inlined)
      LlmPromptConfig.cs                           ← shared prompt + memory budget
      LocalModelSettings.cs / RemoteModelSettings.cs
      PlayerDialogueProfile.cs
    Llm/
      LocalLlmClient.cs                            ← HTTP client (Ollama + OpenAI-compat)
      LlmRequestConfig.cs                          ← unified request struct
      SceneLlmManager.cs                           ← scene singleton, provider routing
      NpcReplyEnvelope.cs                          ← JSON contract + TryParse
      NpcPromptTokens.cs                           ← template token substitution
      OpenRouterApiKey.cs                          ← EditorPrefs → env var resolver
    Memory/NpcMemoryStore.cs                       ← inspectable per-slot SO
    Triggers/
      DialogueTriggerChannel.cs                    ← SO event bus
      DialogueTriggerListener.cs / Payload.cs
      Demo/                                        ← example listeners
    World/
      NpcContextRegistry.cs / NpcContextNode.cs    ← scene info → prompt
      WorldLandmark.cs / LandmarkRegistry.cs / LandmarkAnchor.cs
    Interaction/
      NpcAction.cs                                 ← SO base
      Actions/TalkAction.cs, LogAction.cs
      NpcInteractionContext.cs
    Integration/NpcDialogueAuthoringLink.cs        ← scene NpcInteraction → NpcData
    Debug/LlmDebugBus.cs
  Scripts/NPC/Editor/
    NpcAuthoringStudioWindow.cs                    ← Tools/Dialogue/NPC Crafting Studio
    NpcProfileTabView.cs / NpcPromptTabView.cs
    DemoNpcBuilder.cs
    OpenRouterSettingsWindow.cs
    SceneLlmManagerEditor.cs
    NpcMemoryStoreMigration.cs
  UI/Screens/
    LlmDebugWindow/                                ← per-turn debug panel
    NpcInfoHud/                                    ← in-world NPC HUD
Assets/ScriptableObjects/Dialogue/Demo/Demo_LocalModelSettings.asset   ← LocalModelSettings (outside Flynn)
C:\Users\DriveSIM\models\qwen35\2b\                ← GGUF + Modelfile (outside repo)
```
