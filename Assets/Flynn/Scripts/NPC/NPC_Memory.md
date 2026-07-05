# Flynn NPC Memory & Community Knowledge System

State doc for the LLM-NPC dialogue + memory subsystem. Read this before touching
`Assets/Flynn/Scripts/NPC/`; update it when you change the system (see end).

---

## What it is

NPCs hold live LLM conversations with the player. Each turn, the system:
1. resolves which authored content is relevant (NPC persona, community, the
   world-thing being discussed, trust-gated knowledge, fireable signals),
2. **semantically recalls** the most relevant memories + knowledge from a vector
   store,
3. builds a system prompt, sends chat history + the player line to a local
   (Ollama) or remote (OpenRouter) chat model,
4. parses a strict JSON envelope back (reply, memory updates, fired signals,
   suggested replies), persists new memories (embedded), and raises gameplay
   signals.

Authored community content (community, world-things, NPCs, signals, knowledge)
is **canonically authored as JSON** under
`Assets/Flynn/Configs/NPC/Islands/<island>.json`, and **mirrored into a unified
LiteDB** at runtime. Dynamic data (memories, chat, fired triggers) accrues in the
same DB. The DB is the runtime source of truth; JSON stays the git-canonical
seed and the `island-designer` skill's output format.

---

## Data flow

```
Island JSON (git canonical)
   │  IslandContentHub.Awake → JsonUtility → IslandContent POCOs   (seed/fallback)
   ▼
SceneLlmManager.Start → SeedThenHydrate:
   1. IslandDbImporter.Import(content, db, embedder)
        → upsert community/things/npcs/signals/knowledge into LiteDB
        → embed new/changed rows (Ollama all-minilm, 384-dim), idempotent by ContentHash
   2. IslandContentHub.LoadFromDatabase(db, islandId)  (via IslandContentDb.ToContent)
        → rebuild IslandContent POCOs FROM the DB  → BuildIndices
   (+ BackfillMemoryEmbeddings: embeds any vector-less memory rows, e.g. migrated)
   ▼
Dialogue turn (DialogueManager.HandleAgentTurn):
   resolve ctx (NpcContextResolver) → embed query → MemoryDb.Recall(top-k)
   → BuildSystemPrompt (persona + community + resolved block + recalled facts + JSON contract)
   → LocalLlmClient.GenerateReply(chatHistory + playerInput)
   → NpcReplyEnvelope.TryParse → store memory_updates (embedded) + raise triggers_fired
```

If `embeddingSettings` is unassigned, semantic memory is **disabled** and the
system falls back to the legacy `NpcMemoryStore` ScriptableObject path
(keyword/Jaccard recall, chat history from the SO). This fallback is intact on
purpose — don't delete it.

---

## Diagram

```
AUTHORING (git-canonical)
  island-designer skill ─┐
  Community Editor      ─┼──> Configs/NPC/Islands/<island>.json
  hand edit             ─┘    (community · things · npcs · signals · knowledge)
                                     │ Play: SceneLlmManager.Start
                                     ▼
            IslandDbImporter: upsert rows (idempotent by ContentHash)
                             embed text ──► Ollama all-minilm:l6-v2 (384-float)
                                     ▼
   ╔══════════════════════════════════════════════════════════════╗
   ║ LiteDB · npc_memory/<slot>/npc_memory.db                       ║
   ║ AUTHORED (reseeded from JSON)        LIVED (player-made)        ║
   ║  community[vec] things[vec]           memories[vec]             ║
   ║  npcs[vec] signals knowledge[vec]     chat_turns   meta         ║
   ╚═══════════╦══════════════════════════════════════▲═════════════╝
              │ hydrate (IslandContentHub.LoadFromDatabase)│ write-back
              ▼                                            │
   IslandContent POCOs ──► NpcContextResolver (thing? trust-gated knowledge? signals?)
                                     │
   DIALOGUE TURN (DialogueManager.HandleAgentTurn):
     player line ─► embed query ─► NpcMemoryDatabase.Recall
                                   cosine over this npc's memories + visible knowledge
                                   → top-k (sim + importance + recency)
     system prompt = persona + community + resolved thing + recalled facts
                     + JSON contract + recent chat history
                                     ▼
                       LocalLlmClient ─► chat LLM (Ollama / OpenRouter)
                                     ▼
                       NpcReplyEnvelope (JSON)
                         reply           → player
                         memory_updates  → embed + dedup ─► new MemoryDoc[vec] back into DB
                         triggers_fired  → raise signals
```

Loop: JSON seeds the DB → DB hydrates the runtime → each turn recalls the most
meaning-relevant rows → the reply writes new embedded memories back into the same
DB. Authored lore and lived memory share the same shape, so recall draws from both.

## LiteDB store

- File: `Application.persistentDataPath/npc_memory/<slot>/npc_memory.db`
  (e.g. `…/LocalLow/DefaultCompany/Solarpunk/npc_memory/slot_0/`). One DB per
  save slot. LiteDB is pure C# (`Assets/Flynn/Plugins/LiteDB.dll`,
  netstandard2.0) — no native deps. Opened in **Shared** mode so the editor
  Memory Browser can read while Play writes.
- Wrapper: `NpcMemoryDatabase` (`Runtime/Memory/NpcMemoryDatabase.cs`). Owns
  connection lifecycle, typed collections, generic `GetCollection<T>` for future
  doc types, and `Recall` (brute-force cosine over a Find-filtered candidate set
  — fast at NPC scale, no native vector index).

### Collections (docs in `Runtime/Memory/MemoryDocs.cs`)

Authored (mirrored from JSON; `IslandId` + `ContentHash`; reseeded on JSON change):
- `community` `CommunityDoc` — embedded(overview+situation)
- `things` `ThingDoc` — embedded(name+aliases+desc)
- `npcs` `NpcDoc` — embedded(name+role+style+traits); `DesignerNotesJson` raw
- `signals` `SignalDoc` — not embedded
- `knowledge` `KnowledgeDoc` — embedded(text); `OwnerScope` = npcId | "community"

Dynamic (player-created; never touched by re-import):
- `memories` `MemoryDoc` — Subject(player|self|world|relationship|disclosure),
  Importance, RecallCount, `Source`, embedded
- `chat_turns` `ChatTurnDoc`
- `meta` `MetaDoc` — SchemaVersion, EmbeddingModel, Dim (warns on model mismatch)

### Text + vector live together

Every embedded row stores **both the original text and its vector**, side by side:

```
MemoryDoc {
  Text:      "Maren told Flynn the wind shrine seals the tunnel"  ← source of truth, goes in the prompt
  Embedding: [0.0123, -0.0456, … ]  (384 floats)                  ← search key only, never shown
  Subject, Importance, RecallCount, CreatedUtc, NpcId, Source …
}
```

- **Embedding** is used only at recall time: embed the query, cosine-compare
  against stored vectors to *find* relevant rows. Never sent to the LLM.
- **Text** is the payload: the chosen rows' `Text` is what gets pasted into the
  prompt's "Known facts:" block.
- The vector is **derivable** from the text — that's why "re-embed" / backfill
  can null it and regenerate. Text is canonical; the vector is just the index.

Embeddings stored as `float[]` on the doc (BSON array). `VectorMath.Cosine`
guards null/length/zero-norm.

---

## Embedding

- `IEmbeddingProvider` (`Runtime/Memory/Embedding/`) — coroutine `Embed(text, cb)`.
- `OllamaEmbeddingProvider` — `POST /api/embeddings {model, prompt}` → `float[]`.
  Model **must include the tag**: `all-minilm:l6-v2` (Ollama 404s on bare
  `all-minilm`). `ollama pull all-minilm` installs the `:l6-v2` tag.
- `EmbeddingSettings` SO (`Runtime/Data/EmbeddingSettings.cs`) — endpoint, model,
  dim, `recallTopK`, `minSimilarity`, `dedupThreshold`. Asset:
  `Configs/NPC/Embedding_Global.asset`. Assigned on `SceneLlmManager`.
- Future: swap in an in-process `SentisEmbeddingProvider` (ONNX) behind the same
  interface to ship without a server — would need a C# BERT WordPiece tokenizer.

---

## Key runtime files

| File | Role |
|------|------|
| `Runtime/Llm/SceneLlmManager.cs` | Scene hub. Owns DB + embedder lifecycle, seed-then-hydrate, backfill, provider config. `SemanticMemoryReady` gates the DB path. `ContentHash(...)` helper. |
| `Runtime/DialogueManager.cs` | Per-turn orchestration. Semantic recall + DB writes when ready; SO fallback otherwise. UI Toolkit dialogue panel. |
| `Runtime/Content/IslandContentHub.cs` | Holds `IslandContent` POCOs + id indices. `LoadFrom(json)` (seed) and `LoadFromDatabase(db,islandId)` (runtime source). |
| `Runtime/Content/IslandDbImporter.cs` | JSON POCOs → DB rows, embeds new/changed (coroutine). |
| `Runtime/Content/IslandContentDb.cs` | DB ↔ `IslandContent` conversion (`ToContent` used by hub + editor export; `SeedAuthored` writes rows without vectors). |
| `Runtime/Content/NpcContextResolver.cs` | Picks the relevant content slice per turn (thing by explicit/proximity/alias; trust-gated knowledge; fireable signals). Unchanged by the DB work — reads the hub. |
| `Runtime/Content/IslandPromptBuilder.cs` | Renders persona/community/resolved blocks. |
| `Runtime/Llm/LocalLlmClient.cs` | Chat HTTP (Ollama native + OpenAI/OpenRouter), `<think>` stripping. |
| `Runtime/Llm/NpcReplyEnvelope.cs` | Strict JSON contract + tolerant parser. |
| `Runtime/Memory/NpcMemoryStore.cs` | **Legacy** SO store — fallback + migration source only. |
| `Runtime/Memory/RecalledKnowledgeChannel.cs` | SO event channel. `DialogueManager` raises it each turn with the recalled items (copy of `RecalledItem`); the NPC Info HUD subscribes (`OnRaised`) to show which knowledge/memories were sent to the LLM for the last player input. Asset: `Configs/NPC/RecalledKnowledge_Channel.asset`, assigned on `SceneLlmManager.recalledKnowledgeChannel`. |

## Key editor files

| File | Menu | Role |
|------|------|------|
| `Editor/NpcMemoryBrowserWindow.cs` | Flynn → NPC → Memory Browser | Read/manage the runtime DB per slot: memories/knowledge/chat/things/npcs/signals, delete / clear / re-embed. |
| `Editor/FlynnCommunityEditorWindow.cs` | Flynn → NPC → Community Editor | DB-schema-aware authoring of the island JSON with reference dropdowns + "referenced by" link views + validation + JSON save. The single authoring editor (replaced the old `IslandContentEditorWindow`). |
| `Editor/NpcMemoryDbMigration.cs` | Flynn → NPC → Migrate Memory Store → DB | One-off: legacy `NpcMemoryStore` → DB (vectors backfilled at next Play). |
| `Editor/SceneLlmManagerEditor.cs` | — | Custom inspector. **Add new `SceneLlmManager` fields here or they won't render.** |

---

## Authoring workflow

1. Author/edit island JSON (skill `island-designer`, or the Community Editor, or
   by hand). JSON is the git-canonical source; validate with
   `IslandContentValidator`.
2. Play: the importer mirrors JSON → DB and embeds; the hub hydrates from DB.
3. JSON edits propagate to existing save slots on next Play (authored rows are
   reseeded by `IslandId`; dynamic memory is preserved). For a clean baseline,
   delete the slot DB or use a new `saveSlotId`.

---

## Gotchas

- **Custom inspector**: `SceneLlmManager` fields only show if added to
  `SceneLlmManagerEditor` (hand-drawn). Same trap will bite any new field.
- **LiteDB.dll meta**: must be **enabled for Editor** (PluginImporter Editor
  platform `enabled: 1`) or every `using LiteDB;` file fails to compile.
- **Ollama model tag**: embeddings need `all-minilm:l6-v2`, not `all-minilm`.
- **ContentHash scheme changes** invalidate idempotency → re-import inserts fresh
  rows alongside old ones. Wipe the slot DB when you change hashing.
- **IL2CPP**: LiteDB uses reflection. Player (IL2CPP) builds will likely need a
  `link.xml` to prevent stripping. Editor/Mono is fine. Not yet added.
- **Re-embed**: nulling a memory's vector → `BackfillMemoryEmbeddings` re-embeds
  next Play. Backfill currently covers `memories` only, not `knowledge`
  (knowledge re-embeds when its JSON text changes).
- Recall is async (one embed round-trip per turn) — done inside the existing
  `HandleAgentTurn` coroutine before building the prompt.
- **Follow-up anchoring**: `DialogueManager.BuildRecallQuery` embeds the player
  line *plus*, for anaphoric follow-ups only (`IsFollowUp`: ≤3 words, or 4–8 words
  with a referential cue like "more/it/that/why"), the previous turn's `topic` +
  truncated NPC reply. Stops bare "tell me more" recalling junk. Substantive
  inputs (>8 words) stay un-anchored to avoid topic-smear. Still one embed/turn.

---

## Extension points (designed-for, not yet built)

- `RelationshipDoc` / `PlayerStateDoc` via `NpcMemoryDatabase.GetCollection<T>` —
  persist full relationship state (not just trust via PlayerPrefs) and inject
  live player state (inventory/flags/location) into prompts.
- Cross-NPC gossip / shared world memory; fact consolidation/summarization.
- Surface `things`/`npcs` docs themselves in `Recall` (one filter addition) so
  world-things are semantically recalled, not just authored knowledge about them.

---

## Built systems (previously extension points, now implemented)

### Trust persistence

Trust persists via `PlayerPrefs` (`Flynn.NPC.Trust.{npcId}`). `NpcRelationshipState`
loads on `Awake`/`Start` (falls back to `startingTrust` if no saved value).
`AdjustTrust()` saves on every change. The system prompt shows the real current
trust: `"current trust 22/100 (started at 20)"` via `IslandPromptBuilder.BuildPersonaBlock`.

### Relationship deltas

`DialogueManager.ApplyRelationshipDeltas` applies `relationship_deltas` from
the LLM envelope to `NpcRelationshipState` (trust/affection/suspicion). Shows
floating "+N"/"-N" text, plays audio, checks trust milestones (secret unlocks).

### World state awareness

`DialogueManager.GatherWorldState()` queries gameplay objects each turn:
- Transmitter power level (`TransmitterStation.Power`)
- Solar collectors cleaned count (`SolarCollector.IsCleaned`)
- Signal relay status (`SignalRelay.IsActivated`)

Injected into the system prompt via `IslandPromptBuilder.BuildResolvedBlock`
as a "World state right now:" block. The LLM can now react to player progress.

### Gameplay-driven objective completion

Gameplay scripts call `ObjectiveTracker.CompleteFromGameplay(signalId)` when
milestones are reached:
- `SolarCollector.Clean()` → checks if all collectors cleaned → completes
  `objective.restore_steady_power`
- `SignalRelay.Activate()` → completes `objective.activate_relay` +
  `complete.relay_activated`

Completion persists via PlayerPrefs (`Flynn.Objective.Completed.{signalId}`)
and marks the signal as fired in the DB so the LLM doesn't re-fire it.
`LoadFiredObjectivesFromDb` restores the correct state (Active vs Completed)
on startup.

### Player-facing objective titles

`SignalContent` has a `title` field (island JSON). `ObjectiveTracker.ResolveTitle()`
prefers `title`, falls back to `description`, then `signalId`. Used by
`UnlockObjective`, `LoadFiredObjectivesFromDb`, and `CompleteFromGameplay`.

### Objective tracker persistence

`ObjectiveTracker` loads already-fired objective signals from the DB on `Start()`
via `LoadFiredObjectivesFromDb`. `NpcMemoryDatabase.GetAllFiredSignals()` returns
all `FiredSignalDoc` records. Gameplay-completed objectives are restored as
`Completed` (not `Active`) via `WasGameplayCompleted(signalId)` PlayerPrefs check.

### Live codex refresh

`ObjectiveTracker.OnObjectivesChanged` event fires on unlock/complete.
`CodexPanelController` subscribes to auto-refresh the Tasks tab when objectives
change while the codex panel is open.

---

## Deferred features (not yet built)

- **Encrypted scan → NPC translation loop**: `AddScanFragment`/`TranslateScanFragment`
  exist on `PlayerCodex` but no world-placed encrypted scan targets beyond the
  weather station.
- **Multi-NPC codex support**: currently single-NPC focused; codex needs to handle
  multiple NPCs with separate secret pools.
- **Deflection UI**: visual feedback when NPC avoids a topic due to low trust
  (player doesn't know it's trust-gated).
- **NPC acknowledges prior knowledge**: codex entries not currently injected into
  the LLM prompt (NPCs don't know what the player already knows from the codex).
- **Trust from gameplay actions**: trust currently only rises via LLM deltas, not
  from gameplay milestones (feeding transmitter, cleaning collectors, etc.).
- **"Return to NPC" loop**: no "player has completed X since last visit" context
  injection on dialogue re-open.
- **Ambient NPC dialogue**: no idle barks, no calling out when player walks by.
- **Codex as dialogue tool**: no way to reference codex entries in dialogue to
  confront/challenge NPCs.
- **In-process SentisEmbeddingProvider (ONNX)**: would remove the Ollama server
  dependency for player builds.
- **IL2CPP `link.xml`**: needed for LiteDB reflection in player builds.

---

## When you change this system

Update this file. If you add a `SceneLlmManager` field, also add it to
`SceneLlmManagerEditor`. If you change the DB schema, bump
`NpcMemoryDatabase.SchemaVersion` and note the migration here.
