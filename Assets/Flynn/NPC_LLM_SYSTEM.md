# LLM NPC Dialogue System — Onboarding

Read this before touching anything under `Assets/Flynn/Scripts/NPC/`. Companion: `llm_training_log.md` (tuning record).

**Last verified against code: 2026-07-03.** The 2026-05-31 revision of this file described a retired architecture (NpcData SO, NpcMemoryStore, Time.timeScale pause, Maren-as-scene-NPC) — all of that is gone. If this doc and code disagree, code wins; re-verify with a grep before building on any claim here.

---

## 1. What this system is

Conversational NPCs backed by an LLM. Player talks in free text or clicks suggested-reply chips; the model answers **in character** as a structured JSON envelope — one call per turn. Content is authored per island as JSON (story-bible pipeline), imported into a LiteDB database, and recalled semantically per player input.

---

## 2. Scenes & content (current)

| Scene | Island JSON | NPCs |
|---|---|---|
| `2D_Lighting_Demo.unity` (main game) | `first_light.json` | `npc.transmitter_station` (ion_TransmitterStation) |
| `NPC_Sandbox.unity` (NPC dev) | `windroot_hamlet.json` | 7 NPCs incl. `npc.maren_wind_keeper` |

Scene wiring: `NPC_LLM` GO holds `IslandContentHub` (parses island JSON in Awake) + `SceneLlmManager` (provider routing, `SeedThenHydrate`: `IslandDbImporter.Import` embeds + seeds Knowledge/Things/Signals into the DB, then `hub.LoadFromDatabase` makes the DB the runtime source — SceneLlmManager.cs:110-126). Scene NPCs carry `NpcAuthoringLink` (npcId + portrait; replaces retired NpcData/NpcDialogueAuthoringLink), `NpcInteraction`, `NpcClickInteraction`, `NpcRelationshipState`. `WorldThingLink` connects scene objects to island "things".

## 3. Memory (LiteDB — the only store)

- `NpcMemoryDatabase` (LiteDB.dll) at `Application.persistentDataPath/npc_memory/slot_<n>/npc_memory.db` — persists across sessions AND player builds.
- Semantic recall: `OllamaEmbeddingProvider` (all-minilm, 384-dim, `EmbeddingSettings` SO); per player input, `Recall` blends memories + trust-gated knowledge (owner + community) into a "Known facts:" prompt block (DialogueManager.cs:206-225, 529-543).
- **Build gotcha:** embeddings require a local Ollama even in shipped builds — otherwise SILENT degrade to keyword `BuildMemorySummary` (DM:381-383). No retry logic exists on LLM calls (single shot, UnityWebRequest timeout only).
- `NpcMemoryStore_slot_0.asset` is a dead asset from the retired SO-store era; no class backs it.

## 4. DialogueManager flow (verified)

1. Open via NpcInteraction/click → `IsDialogueOpen` static flag + `LockPlayerMovement` (NO timeScale pause).
2. Player input (free text or chip; chips also on number keys 1-4).
3. System prompt: global config + persona/community/resolved blocks (`IslandPromptBuilder`, DM:359-377) + semantic recall block + player profile + JSON contract last.
4. Single POST via `LocalLlmClient.GenerateReply` (URL-routes Ollama `/api/chat` vs OpenAI-compat `/v1/chat/completions`).
5. `NpcReplyEnvelope.TryParse` (fence/brace tolerant). Empty/error/parse-fail → in-character `"[Fallback] " + fallbackReply`.
6. While waiting: `ThinkingDotsRoutine` ("Thinking./../...") ; reply renders via typewriter (`WaitForSecondsRealtime`), click/key to skip.
7. `memory_updates[]` → DB; `triggers_fired` validated against allowlist (repeatable/already-fired checks) → `DialogueTriggerChannel.Raise` (DM:679-718); fired signals persisted in DB.
8. Trust UI: bar, floating deltas, secrets count, milestone pulse (DM:772-863). Envelope affection/suspicion deltas exist but are NOT surfaced in UI (NpcInfoHud hardcodes 0).

## 5. Envelope (unchanged contract)

`reply, topic, intent, tone, mood_shift, relationship_deltas{trust,affection,suspicion}, flags[], suggested_player_replies[3], memory_updates[{subject,fact}], triggers_fired[]` — deltas int -2..+2; subjects player/self/world/relationship/disclosure; invented trigger keys dropped w/ warning.

## 6. Signal consequences (current truth)

- `ObjectiveTracker` (MANAGERS): UnlockObjective/CompleteObjective, PlayerPrefs + DB-restored, on-screen chips — WORKS.
- `TutorialSignalHandler`: `tutorial.first_feed / first_solar / scan_weather / relay_activated` — **log-only stubs**, no world reaction yet.
- Demo listeners: `DialogueTriggerSealEffect`, `PillarPushReaction`, generic `DialogueTriggerListener` (UnityEvent).

## 7. Providers

- **Current default: OpenRouter** (`provider: 1` in scene) — `Assets/Flynn/OpenRouter/Default.asset`: `openai/gpt-4o-mini`, forceJsonMode, proxy `http://127.0.0.1:10808`, key via EditorPrefs (`Flynn → NPC → OpenRouter Settings`) or env `OPENROUTER_API_KEY`.
- Local Ollama path exists (`LocalModelSettings`, qwen custom builds — see llm_training_log.md) but is not the scene default.

## 8. Shipped 2026-07-03 (same-day session — conversation-as-mechanic pass)

- **Verb chips (A):** envelope gained `suggested_reply_verbs[3]` (ask|press|joke|show, parallel array, back-compat). Prompt rules: press = risky push (suspicion+ on avoided topics, may yield secrets at high trust), joke = affection play, max one press offered. Chips render `[!]`/`[~]` glyph + colored left stripe; number keys submit clean text via `userData`.
- **World reacts mid-dialogue (E):** `TutorialSignalHandler` (Scripts/Tutorial) rebuilt — the 4 tutorial signals fire staggered scale-pulse + whoosh + chime chains on their scene objects (relay = big variant + micro-shake). Targets auto-found by name.
- **Barks + ! (F):** `NpcBarkController` + `BarkBubble` (Runtime/World). Event-driven world-space bubbles (TransmitterFed / ResourceDepleted in range / BatteryLow), cooldowns + chance, silent during dialogue, "!" TMP indicator until next dialogue open. On ion_TransmitterStation in the scene. NOTE: bus subscribers off-MANAGERS need the OnEnable+Start retry pattern (Instance null race).
- **Topics + trust gates (B):** dialogue topic strip — codex "Ask: X" chips + `[locked] kind — trust N` teases (nearest thresholds, never Avoid kinds), refreshed per turn. DB: `NpcMemoryDatabase.GetKnowledgeMeta(npcId)`.
- **Show + gift (C):** items row in dialogue (icons, counts). Show = context line, item stays; gift = removes one, prompt rules award trust/affection + memory_update. Wrench never giftable.
- **UI redesign:** `DialogueBox.uxml/uss` rebuilt — 62% bottom sheet, header (name + ticked trust bar + secrets + codex/close), 3-column body w/ framed portraits, conversation ScrollView (finally in UXML), labeled TOPICS│ITEMS context strip, caret input. `PlayerHud` replated (see UI/Screens/PlayerHud). All 15 DialogueManager query names preserved; topics/items rows bind to predefined containers w/ code fallback.

## 9. Known gaps (2026-07-03)

- **NEXT UP (user-flagged): codex/memory discrimination.** `PlayerCodex.OnDialogueTurnCompleted` captures ALL `memory_updates` indiscriminately (code comment admits it) — every filler fact lands in the journal and DB. Needs a quality/relevance gate: importance scoring, dedupe beyond exact hash, subject filtering, maybe model-side "codex_worthy" flag in the envelope.
- Player portrait art: `Player_Flynn.asset` `portraitSprite` is null (profile IS wired to SceneLlmManager) — assign a sprite and it shows in the new frame. NPC portraits via `NpcAuthoringLink.portraitSprite`.
- No LLM retry; no timeout-specific in-character line.
- `NpcInfoHud` disabled in main scene; affection/suspicion never displayed anywhere.
- Embedding dependency in builds (see §3 gotcha).
- Verb-chip + gift reaction quality vs gpt-4o-mini not yet validated in a real conversation.

## 9. File map (updated)

```
Assets/Flynn/Scripts/NPC/Runtime/
  DialogueManager.cs                 orchestrator (thinking dots, typewriter, chips, trust UI)
  NpcInteraction.cs / NpcClickInteraction.cs / NpcRelationshipState.cs
  NpcAuthoringLink.cs                scene NPC → npcId + portrait (CURRENT authoring link)
  Llm/  LocalLlmClient.cs, SceneLlmManager.cs, NpcReplyEnvelope.cs, OpenRouterApiKey.cs
  Memory/ NpcMemoryDatabase.cs (LiteDB), OllamaEmbeddingProvider.cs, EmbeddingSettings
  Island/ IslandContentHub.cs, IslandDbImporter.cs, IslandPromptBuilder.cs (paths approx — grep first)
  Triggers/ DialogueTriggerChannel.cs, DialogueTriggerListener.cs, demo listeners
Assets/Flynn/Configs/NPC/Islands/ first_light.json, windroot_hamlet.json
Assets/Flynn/PreProduction/Bibles/ story bibles → island JSON pipeline
MANAGERS: ObjectiveTracker, PlayerCodex; UI/Screens/: NpcInfoHud, LlmDebugWindow (F9)
```
