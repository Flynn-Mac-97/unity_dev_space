# LLM NPC Dialogue System — Onboarding & Memory

Onboarding doc for the Flynn LLM-driven NPC dialogue system. Read this before touching anything under `Assets/Flynn/Scripts/NPC/`. Companion files: `Assets/Flynn/CLAUDE.md` (Flynn-wide rules), `Assets/Flynn/llm_training_log.md` (the tuning experiment record).

Last updated: 2026-05-29 (split-pipeline pivot landed).

---

## 1. What this system is

A local-LLM-driven conversational NPC system. The player talks to an NPC in free text; a local model (via Ollama) replies **in character** and emits structured side-data (relationship deltas, the topic discussed, and gameplay "trigger" events). Designer-authored ScriptableObjects define each NPC's personality, knowledge, secrets, relationship thresholds, and triggers. The design goal is **interesting, fantasy-grounded, alive-feeling dialogue that doesn't read as generic AI-chatbot output** — and the *system* must work across many personality types (warm, dry, rude), not just one.

Test character throughout: **Maren_WindKeeper** — a Ghibli-warm, watchful island wind-keeper.

---

## 2. Models (Ollama, local)

Built from ModelScope GGUFs (Ollama's own download was too slow). Files + Modelfiles live OUTSIDE the repo at `C:\Users\DriveSIM\models\qwen35\{0.8b,2b}\`.

| Ollama tag | Base | Quant | Status |
|---|---|---|---|
| `npc-qwen35-2b` | Qwen3.5 2B | Q4_K_M | **Primary dialogue model.** Use this for tuning. |
| `npc-qwen35-0.8b` | Qwen3.5 0.8B | Q5_K_M | Too weak for schema+persona — stutters/recites. Kept only as a speed-floor comparison. |

Modelfile essentials (both):
- **Qwen ChatML `TEMPLATE`** (copied from `qwen3:4b-instruct`). The raw GGUFs shipped with no chat template, so Ollama defaulted to `{{ .Prompt }}` and ignored system/role messages entirely → models hallucinated wildly. Fixing the template was essential.
- A **short, positive-only** SYSTEM prompt. Negation lists ("never mention X, never mention Y…") made the small models *recite the forbidden list in a loop*. Keep Modelfile system prompts positive.
- `keep_alive 30m`, `repeat_penalty 1.15`, `num_ctx 8192`, ChatML stop tokens.

Qwen3.5 0.8B/2B have **native JSON mode + function calling + 262K context** (released 2026-03-02). The 2B handles our `format` schema reliably.

---

## 3. Runtime architecture

### Data layer (ScriptableObjects)
Scripts in `Scripts/NPC/Runtime/Data/`. NPC config assets in `Assets/Flynn/Configs/NPC/`.

- **`NpcDialogueAgentConfig`** — per-NPC root. References personality, prompt template, memory settings, knowledge base, relationship defaults, a list of `DialogueTriggerDef`, and `NpcGameplayRoles` flags. (Maren: `Configs/NPC/Maren_WindKeeper.asset`.)
- **`NpcPersonalityProfile`** — identity, voice, traits, do/don't rules, portrait, `conversationHooks` (things the NPC is keen to bring up — these *motivate* the model toward trigger-able moments), fallback lines.
- **`NpcKnowledgeBase`** — four buckets (`knownFacts`, `beliefs`, `rumors`, `secrets`), each a `KnowledgeEntry` (topic + text + `RevealCondition` + threshold). Plus `avoidedTopics`.
- **`DialogueTriggerDef`** — a fireable gameplay event. `Key` = asset filename (no typos). Kinds: StoryBeat/ClueReveal/OneTime/Repeatable/Forbidden/Secret/Misdirection.
- **`NpcPromptTemplate`** — token-based system-prompt skeleton (`{npc_name}`, `{relationship_summary}`, `{available_clues}`, etc.).
- **`NpcRelationshipDefaults`** — starting trust/affection/suspicion + `trustToShareClues` / `trustToShareSecrets` thresholds.
- **`PlayerDialogueProfile`** — who the player is (Maren talks to `Configs/Player/Player_Flynn.asset`).
- **`LocalModelSettings`** — endpoint, **dialogue** model name + sampling, **classifier** model name + sampling, timeout. Live asset: `Assets/ScriptableObjects/Dialogue/Demo/Demo_LocalModelSettings.asset` (outside Flynn). Currently: dialogue `npc-qwen35-2b` temp 0.7 maxTokens 320; classifier `npc-qwen35-0.8b` temp 0.1 maxTokens 64; timeout 45; endpoint `http://127.0.0.1:11434/api/chat`.

### Runtime flow ([DialogueManager.cs](Scripts/NPC/Runtime/DialogueManager.cs))
The pipeline is now **one dialogue call + three classifier calls per turn**. The 2B model only writes the reply; the 0.8B model does all post-processing against closed vocabularies.

1. `OpenAgent(config, fallbackData, relationship)` binds the UIToolkit panel and loads persisted memory.
2. `SubmitPlayerInput` → `HandleAgentTurn` coroutine. Every turn goes the same path (no goodbye special-casing).
3. **Dialogue call** — `BuildSystemPrompt` assembles persona + scored relevant clues + eager-to-share hooks + player profile + conversation-progress block + `NpcLlmResponseParser.ReplyOnlyInstructions`. No schema, no triggers list, no delta rules. `LocalLlmClient.GenerateReply` → Ollama `/api/chat`, **plain text output**. Chat history (last N turns) sent as real user/assistant messages.
4. **Topic classifier** — `LlmClassifier.PickOne` on the NPC's reply. Options = display names of every topic referenced by the NPC's knowledge base (knownFacts/beliefs/rumors/secrets) plus `none`. Ollama `format` enum constrains output.
5. **Events classifier** — `LlmClassifier.PickMany` on the NPC's reply. Options = gate-eligible trigger keys (drafts excluded; Forbidden excluded; one-time-already-fired excluded; Secret-kind excluded when trust < `trustToShareSecrets`). Trigger descriptions are appended as `"What each event means:"` context.
6. **Deltas classifier** — `LlmClassifier.EstimateDeltas` on the **player's input** (not the reply) plus a short persona summary (name, role, traits, current trust/affection/suspicion). Returns `{trust, affection, suspicion}` each in -3..+3.
7. The four outputs are composed into `ParsedTurn`. Deltas applied to `NpcRelationshipState`; `events` raised on the `DialogueTriggerChannel` SO (still re-filtered against one-time fires + Forbidden as a belt-and-braces); one-time fires recorded; topic added to `discussedTopics`.

### Key runtime files
- `Runtime/Llm/LocalLlmClient.cs` — HTTP to Ollama for the **dialogue** call. Plain text output now; no schema injection.
- `Runtime/Llm/LlmClassifier.cs` — small-model closed-vocabulary primitive. Three calls: `PickOne(instruction, text, options)`, `PickMany(...)`, `EstimateDeltas(instruction, text)`. Each uses Ollama `format` to enum-constrain the output, so JSON parsing is reliable.
- `Runtime/Llm/NpcLlmResponseParser.cs` — minimal now. Holds `ParsedTurn` (consumed by `GameplayUpdateApplied` listeners) and the `ReplyOnlyInstructions` string. No JSON parsing.
- `Runtime/Llm/NpcPromptContextBuilder.cs` — scores knowledge entries by relevance, builds clues/hooks/player/progress blocks. (Still exposes `BuildAvailableTriggersBlock`, now unused — kept for reference.)
- `Runtime/Llm/SceneLlmManager.cs` — scene singleton: shared `LocalModelSettings`, `saveSlotId`, `PlayerDialogueProfile`.
- `Runtime/Memory/NpcDialogueMemoryStore.cs` — per-NPC + per-save-slot persistence of recent turns, learned facts, fired one-time triggers.
- `Runtime/NpcRelationshipState.cs` — live trust/affection/suspicion MonoBehaviour with `AdjustTrust/Affection/Suspicion`.
- `Runtime/Triggers/DialogueTriggerChannel` — SO event bus scene listeners subscribe to.
- **DELETED:** `DialogueWrapDetector.cs` (goodbye special-casing removed); `NpcLlmResponseParser.Parse` + `JsonSchemaInstructions` + `SchemaInstructions` (replaced by split pipeline); `LocalLlmClient.NpcTurnJsonSchema` and the `format`-injection branch.

### Classifier output schemas (Ollama `format`)
```json
// PickOne   -> { "choice":  "<one of options or 'none'>" }
// PickMany  -> { "choices": ["<allowed>", ...] }
// Deltas    -> { "trust": <-3..3>, "affection": <-3..3>, "suspicion": <-3..3> }
```

---

## 4. Testing

No automated battery runner. Tune by talking to NPCs in-Editor (open the dialogue panel via the demo scene) and reading the `[Dialogue]` console lines, which log per-turn topic / events / deltas / state. Historical pre-split transcripts remain under `Scripts/NPC/.test/cycle_NN/` for reference but are no longer regenerated.

---

## 5. Tuning results so far (pre-split — see llm_training_log.md for detail)

All cycles: 20/20 valid JSON, 0 errors, ~1.6–1.7 s/turn warm, ~32 s total.

- **Cycle 1 (baseline):** Leaked the cave secret, the Storm Year (forbidden topic), and the husband backstory — all at low trust. Root cause: the LLM was **parroting the worked examples in the prompt** (which contained real lore) and reciting knowledge-base text verbatim. Voice flat/chatbot-ish in many turns.
- **Cycle 2 (changes A,B,C):** Replaced lore-bearing examples with shape-only generic ones; filtered `avoidedTopics` out of the clues block; removed the husband fact from Maren's `roleDescription`. Husband leak fixed. But the cave secret still leaked **via the trigger description text**, and Storm Year still leaked.
- **Cycle 3 (changes D,E,F):** Gated `Secret`-kind triggers behind `trustToShareSecrets` (so their content-bearing descriptions don't appear in-prompt until earned); reworded `dontRules` to not name the Storm Year; removed a contradictory "Reply in text only" line. **All three leaks fixed.** New problem appeared: **intra-reply stuttering** (e.g. "I do not know what opens it." ×3) — a sampler-collapse symptom with `repeat_penalty 1.0`. Also achieved the target voice once (T3 layered a known-fact with a belief in Maren's voice).
- **Cycle 4 (changes G,H,J — APPLIED TO CODE, NOT YET TESTED):** `repeat_penalty` raised to 1.15 (in both `LocalLlmClient` and the runner); added an explicit "no internal phrase repetition" rule and a "forbidden topic → suspicion +1/+2" rule to `JsonSchemaInstructions`. **The Cycle 4 battery was never run** (paused for token budget).

Persistent weak spots: relationship deltas often 0 (apathetic), occasional invented details not in the knowledge base, self-contradiction across turns (short chat-history window), trigger fire-timing imperfect.

---

## 6. Architectural decision — RESOLVED 2026-05-29

The 2B model was overloaded: dialogue **and** topic/events/deltas in one constrained call → leaks, wrong triggers, stuttering, flat deltas. We split it.

**Decision (built and shipped):**
- **Dialogue → 2B (`npc-qwen35-2b`).** Plain text only, lean prompt: persona + scored relevant clues + hooks + player profile + progress + `ReplyOnlyInstructions`. No schema, no triggers list, no delta rules.
- **Topic, events, deltas → 0.8B (`npc-qwen35-0.8b`).** Each is a separate `LlmClassifier` call with a closed vocabulary, enforced by Ollama's `format` enum. The 0.8B can't pick anything outside the authored option list.
- **Per-NPC vocabulary is implicit in the existing SOs:** topic options = display names of topics referenced by the NPC's knowledge buckets; event options = gate-eligible triggers (same Forbidden / one-time-fired / Secret-trust-gate filters as before). No new SO types needed — designers keep authoring `Topic` and `DialogueTriggerDef` assets and the classifier picks from those.
- **Deltas are judged directly by the 0.8B against the player's input** + a short persona summary (name, role, traits, current scores). No bucket SOs, no anchor lists — we lean on the model's judgement at low temperature.

**What this buys us:**
- The 2B prompt is ~half the tokens (no schema, no examples, no triggers block) → warmer prose, no parroting of example lore (the C1 cave-secret leak class is structurally gone).
- Classifiers can't hallucinate trigger keys or topics — enum-constrained outputs.
- Each pass tunable independently of the others. Designers can extend Topic/Trigger sets without touching prompts.

**Validation**: hand-driven in-Editor only (no automated battery anymore — runner removed).

---

## 7. Quick reference — file map

```
Assets/Flynn/
  NPC_LLM_SYSTEM.md            ← this file
  llm_training_log.md          ← cycle-by-cycle tuning record
  CLAUDE.md                    ← Flynn-wide rules
  Configs/NPC/Maren_WindKeeper.asset      ← test NPC (multi-object: config+personality+knowledge+template+relationship+memory)
  Configs/NPC/Triggers/trigger.maren.*.asset
  Configs/NPC/Topics/Topic_*.asset
  Configs/Player/Player_Flynn.asset
  Scripts/NPC/Runtime/Data/    ← all the SO definitions
  Scripts/NPC/Runtime/DialogueManager.cs           ← orchestrator
  Scripts/NPC/Runtime/Llm/LocalLlmClient.cs        ← Ollama HTTP for dialogue (2B, plain text)
  Scripts/NPC/Runtime/Llm/LlmClassifier.cs         ← small-model closed-vocab passes (PickOne / PickMany / EstimateDeltas)
  Scripts/NPC/Runtime/Llm/NpcLlmResponseParser.cs  ← ParsedTurn struct + ReplyOnlyInstructions
  Scripts/NPC/Runtime/Llm/NpcPromptContextBuilder.cs ← prompt assembly + scoring
  Scripts/NPC/Runtime/Llm/SceneLlmManager.cs
  Scripts/NPC/Runtime/Memory/NpcDialogueMemoryStore.cs
  Scripts/NPC/Runtime/NpcRelationshipState.cs
  Scripts/NPC/Runtime/Triggers/                    ← DialogueTriggerChannel + listeners
  Scripts/NPC/Editor/                              ← authoring studio + tab views + editors
  Scripts/NPC/.test/cycle_NN/                       ← historical pre-split transcripts (no longer regenerated)
Assets/ScriptableObjects/Dialogue/Demo/Demo_LocalModelSettings.asset  ← live model settings (note: outside Flynn)
C:\Users\DriveSIM\models\qwen35\{0.8b,2b}\          ← GGUFs + Modelfiles (outside repo)
```
