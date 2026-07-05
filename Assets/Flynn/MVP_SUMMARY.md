# Flynn — MVP Game Summary

> Verified 2026-06-29. Reflects the codebase + scene state at time of writing.

## Genre & Identity

**Solarpunk survival-crafting** with **LLM-driven NPC dialogue**. Flat 2D, top-down isometric (XY plane, `Rigidbody2D`, gravityScale=0). Built on Unity 2022.3.62f3 LTS + URP 14.0.12. The player is a repair robot exploring floating islands, tending dormant infrastructure back to life, and forming relationships with AI-voiced NPCs that remember, reveal secrets, and grow through trust.

---

## Core Gameplay Loop (First Light — Tutorial Island)

```
Wake on shore → Chop overgrowth → Collect biomass → Feed processing stations →
Transmitter powers up (flickering) → Clean solar collectors (steady power) →
Talk to transmitter NPC (build trust, unlock secrets) →
Scan weather station → Activate signal relay → Contact another island
```

### Progression Tiers

1. **Gathering** — Chop overgrowth nodes with wrench (LMB swing). Biomass drops auto-collect to inventory.
2. **Power — Biomass** — Feed biomass into processing stations (R key). Transmitter gets flickering, fragmentary power.
3. **Power — Solar (Steady)** — Clean buried/dim solar collectors (interact). Each cleaned panel provides continuous passive power. All 3 cleaned = objective complete.
4. **Relay Activation** — Requires transmitter power ≥ 80 AND all solar collectors cleaned. Activating fires the tutorial-complete story beat.

---

## Player Systems

### Movement

| Feature | Status |
|---------|--------|
| 2D isometric movement (WASD) | ✅ Full |
| Acceleration/deceleration model | ✅ Full |
| Diagonal iso-ratio scaling (2:1) | ✅ Full |
| Speed modifiers (flora slow-down, terrain) | ✅ Full |
| Knockback on resource hit | ✅ Full |
| Movement lock during dialogue/swing | ✅ Full |

### Animation

| Feature | Status |
|---------|--------|
| 16-direction blend trees (112 clips) | ✅ Full |
| Adaptive direction smoothing (snap on large turns, lerp on small) | ✅ Full |
| States: Idle, Run, Swim, Grapple, Carry, Throw, Jump | ⚠️ Partial — only Throw and Jump are triggered by gameplay; Swim/Carry/Grapple never set by code |
| Animation speed scales with movement speed (flora = 50% anim speed) | ✅ Full |

### Elevation (Fake-Z System)

| Feature | Status |
|---------|--------|
| `PlayerHeightState` — visual Y lift, discrete height levels | ✅ Full |
| `ElevationZone` — walk under/stand on elevated tilemap layers with occlusion fade | ✅ Full |
| `ElevationRamp` — smooth tier transitions via trigger zones | ✅ Full |
| `LandingResolver` — validates jump landings against iso grid | ✅ Full |
| Jump controller | ❌ Stub — only gizmos + ground-cell detection; no jump impulse |

### Wrench / Tool Combat

| Feature | Status |
|---------|--------|
| LMB swing with animation-timed hit frame | ✅ Full |
| Swing triggers `ToolSwingStarted` → `ResourceHit` event chain | ✅ Full |
| Camera shake + knockback on resource hit | ✅ Full |
| `ToolEffectivenessTable` (SO multiplier per tool×resource) | ✅ Full |
| `PowerBuildupManager` — charge-release with perfect-zone bonus | ✅ Full |
| Wrench throw | ⚠️ Code scaffolded (ThrownWrench prefab exists, throw anim triggers), but full throw mechanics not wired |
| Dedicated combat system | ❌ `Player/Combat/` folder empty |

### Battery / Power

| Feature | Status |
|---------|--------|
| `RobotBattery` — 0-100, passive drain + action costs | ✅ Full |
| Costs: swing=2, throw=5, grapple=8, scan=5/s, pull=3/s | ✅ Full |
| Low-battery warning + empty events | ✅ Full |
| Infinite battery debug toggle | ✅ Full |
| Solar charging (future pickups) | 🔮 Planned — `AddCharge` exists but no charge source wired |

### Inventory

| Feature | Status |
|---------|--------|
| 4-slot hotbar (slot 0 = wrench, 1-3 = items) | ✅ Full |
| Stack-based item add/remove/consume | ✅ Full |
| Auto-collect vs key-pickup (E) | ✅ Full |
| Currency routing (Echo Shards → IntVariable counter) | ✅ Full |
| Held item visual socket (worldPrefab in hand) | ✅ Full |
| Drop items (G key, with pop impulse + re-collect delay) | ✅ Full |
| Hotbar slot switching (1-4) | ✅ Full |

### Interaction System

| Feature | Status |
|---------|--------|
| `Hoverable` — material-swap outline on hover + scale pop | ✅ Full |
| `Interactable` — key/range/prompt config, UnityEvent OnInteract | ✅ Full |
| `InteractionRouter` — routes key presses to hovered interactables | ✅ Full |
| `PlayerScanController` — hold-to-scan, battery drain, progress UI | ✅ Full |
| `Grabbable` / carry system | ⚠️ Partial — state + prompts only, no carry movement logic |

---

## Resource Gathering

| Feature | Status |
|---------|--------|
| `ResourceNode` — HP, stage sprites, cross-fade transitions | ✅ Full |
| `ResourceNodeConfig` SO — HP, drops, tool type, stage sprites | ✅ Full |
| Drop spawning with arc-to-player trajectory | ✅ Full |
| `DroppedItemMagnet` — auto-collect items fly to player | ✅ Full |
| Echo Shard rolling (per-hit chance) | ✅ Full |
| `HitDebrisBurst` — debris sprites with velocity/gravity/spin/fade | ✅ Full |
| Resource types: Overgrowth, Tree, Stone, Metal Scrap, Tech Trash, Flora | ✅ Full |
| Resource death animation (topple + fade) | ✅ Full |

---

## Transmitter / Power System

| Feature | Status |
|---------|--------|
| `TransmitterStation` — power pool with passive decay | ✅ Full |
| Feed fuel (R key, consumes inventory items per fuel table) | ✅ Full |
| Venture threshold gate (powered → passage opens) | ✅ Full |
| `TransmitterGate` — barrier toggle on power state change | ✅ Full |
| Decoupled via `GameEventBus` events only | ✅ Full |

---

## Tutorial Island — "First Light"

### Tutorial Flow

| Feature | Status |
|---------|--------|
| `TutorialDirector` — beat-based tutorial (swing → gather → scan → power) | ✅ Full |
| `TutorialSignalHandler` — routes LLM dialogue signals to tutorial hints | ⚠️ Partial — only logs; no on-screen hint UI |
| `ProcessingStation` — consume biomass → add transmitter power | ✅ Full |
| `SolarCollector` — clean for steady power, visual dirty→clean | ✅ Full |
| `SignalRelay` — final objective, requires power + all solar cleaned | ✅ Full |
| `ObjectiveTracker` — UI chips for active/completed objectives | ✅ Full |

### Island Objects (8 "Things")

- **Transmitter Station** — central NPC, powers up with biomass/solar
- **Processing Stations** (×2) — biomass → power converters
- **Overgrowth** (×5) — choppable resource nodes, drop biomass
- **Solar Collectors** (×3) — clean for steady power
- **Weather Station** — scan target (3s, lore lines)
- **Wind Chimes** — decorative, emotional barometer
- **Shore Marker** — decorative
- **Signal Relay** — end-game activator

---

## NPC / LLM Dialogue System

### Architecture

| Feature | Status |
|---------|--------|
| `DialogueManager` — singleton, UI Toolkit panel | ✅ Full |
| `SceneLlmManager` — provider selection (OpenRouter/SiliconFlow) | ✅ Full |
| `LocalLlmClient` — HTTP requests to LLM API | ✅ Full |
| `NpcReplyEnvelope` — JSON output contract (reply, trust deltas, signals, memory, suggestions) | ✅ Full |
| Typewriter text + click-to-skip + per-char audio | ✅ Full |
| Suggested replies (3 clickable buttons) | ✅ Full |
| Chat history (max 20 turns) | ✅ Full |
| Chat commands (/save, /clear, /help) | ✅ Full |

### NPC Memory

| Feature | Status |
|---------|--------|
| `NpcMemoryDatabase` — LiteDB persistent storage | ✅ Full |
| Semantic recall via `OllamaEmbeddingProvider` (384-dim, all-minilm) | ✅ Full |
| Brute-force cosine similarity over memories + knowledge | ✅ Full |
| Fallback: keyword-scored chat history summary when embeddings unavailable | ✅ Full |
| Anaphoric follow-up handling ("tell me more") | ✅ Full |

### NPC Trust & Relationship

| Feature | Status |
|---------|--------|
| `NpcRelationshipState` — 0-100 trust, persists via PlayerPrefs | ✅ Full |
| Trust UI: bar, value, secrets counter, floating +/- text, milestone toasts | ✅ Full |
| Trust-gated knowledge (secrets unlock at trust 40/50/60/70/75) | ✅ Full |
| LLM-driven trust deltas (-2..+2 per turn) | ✅ Full |

### Signal System (Story Beats)

| Feature | Status |
|---------|--------|
| `DialogueTriggerChannel` SO — LLM fires signals in reply envelope | ✅ Full |
| `DialogueTriggerListener` — scene objects react to signals | ✅ Full |
| Signal handlers: StoryBeat, TutorialHint, ClueReveal, SocialReveal, RelationshipMilestone, UnlockObjective, CompleteObjective, AmbientRemark | ✅ Full (handlers defined in JSON) |
| Demo effects: seal-break animation, pillar push reaction | ✅ Full |

### Player Codex (Journal)

| Feature | Status |
|---------|--------|
| `PlayerCodex` — persistent knowledge journal | ✅ Full |
| Captures LLM memory_updates as codex entries | ✅ Full |
| Scan fragments (encrypted → translated by NPC) | ✅ Full |
| Secrets revealed / locked count | ✅ Full |
| Unviewed entry counter | ✅ Full |

### Island Content Pipeline

| Feature | Status |
|---------|--------|
| `IslandContentHub` — JSON → runtime lookups (NPCs, things, signals, knowledge) | ✅ Full |
| DB seed-then-hydrate (authored JSON → LiteDB → runtime) | ✅ Full |
| `IslandContentValidator` — schema validation | ✅ Full |
| `IslandPromptBuilder` — builds system prompts from content | ✅ Full |
| `NpcContextResolver` — resolves relevant knowledge for LLM context | ✅ Full |
| Two-stage authoring pipeline (NarrativeDesigner → IslandContentCreator agents) | ✅ Full |

---

## NPC: The Transmitter Station (First Light)

A dormant transmitter with **fragmented identity** — teacher, guardian, relay, companion — that doesn't know which version of itself is real. Speaks in layered, shifting voices. Trust-gated arc:

| Trust | Unlock |
|-------|--------|
| 0-10 | Teaching routines, island facts, overgrowth as resource |
| 15-25 | Remembers people, drawings, chose to wait |
| 40 | Admits the carved name (child named it) |
| 50 | Remembers being sung to (chimes ring from its hum) |
| 60 | Confesses it chose to stay |
| 70 | Identity fear: person→tool or tool→person? |
| 75 | Reconciliation: it was all of them; was loved |

**33 knowledge entries** across fact/belief/rumor/secret/avoid categories. **14 signals** for story beats, tutorial hints, objective unlocks, and reveals.

---

## Second Island: Windroot Hamlet (Designed, Not Implemented in Scene)

Story Bible complete (`PreProduction/Bibles/windroot_hamlet.md`). 7 NPCs (Maren, Jorin, Sela, Old Bram, Tamsin, Coll, Anske). Central tension: "what do we keep, and what do we let the wind carry off?" Island JSON exists but is not the active island.

---

## World & Environment

### Visual Systems

| Feature | Status |
|---------|--------|
| `SpriteSortingManager` — Y-based transparency sort | ✅ Full |
| `SortableSprite` — per-object depth sorting (baseOrder - depth×100) | ✅ Full |
| `MapLayerManager` — tilemap layer sorting (Background/Ground/Props/Foreground) | ✅ Full |
| `WindManager` — global wind shader properties + per-sprite weights | ✅ Full |
| `FloraRuntimeManager` — tilemap flora → instantiated prefabs with wind shake | ✅ Full |
| `FloraContactHandler` — slows player in flora, shakes on velocity | ✅ Full |
| `GrassDecalPlacer` — polygon-scattered decals with min-spacing | ✅ Full |
| `WaterAnimator` — scroll/wave shader animation | ✅ Full |
| `WaterSubmersionRenderer` — submersion mask for water intersection outlines | ✅ Full |

### Custom Shaders (16 total)

`FlynnSprite`, `CharacterSpriteLit`, `PixelLitSprite`, `SpriteLit3D`, `SpriteOutline`, `SpriteDepthOverride`, `ProjectedShadow`, `ProjectedShadow2D`, `GrassEdge`, `GrassEdgeDynamicRocks`, `GrassFill`, `IslandUndersideEdge`, `StyledWater2D`, `WaterFill`, `Wind.hlsl`

### Terrain Effect Zones

| Feature | Status |
|---------|--------|
| `TerrainStateAggregator2D` composable zones | ⚠️ Designed in RUNTIME_FLOWS but code does not exist in scene — `Terrain/` folder absent |
| Mud (0.4× speed), Ice (0.05 decel = slippery), Wind (external force), LowGrip, BlocksJump | 🔮 Planned |

### Environment Objects

| Feature | Status |
|---------|--------|
| `PushableCrate2D` — physics-driven, acts as windbreak | ⚠️ Partial (config only) |
| Floating island chunks / manager | ✅ Prefabs exist |

---

## Effects / Game Feel

| Feature | Status |
|---------|--------|
| `SlashEffect` — procedural crescent arc + hit burst | ✅ Full |
| `CameraShake` — random offset with decay | ✅ Full |
| `HitStop` — timeScale freeze with ease-back | ✅ Full |
| `SpriteFlash` — material flash on hit | ✅ Full |
| `HitImpactFX` — particle burst + flash ring + floating damage number | ✅ Full |

---

## Audio

| Feature | Status |
|---------|--------|
| `AudioManager` — pooled AudioSource round-robin | ✅ Full |
| 10 SFX AudioProfiles (hit, break, swing, pickup, jump, land, grapple, transmitterFeed, batteryLow, batteryEmpty) | ⚠️ All profiles are empty placeholder SOs — no audio clips assigned |
| `CodexAudio` — procedural tones (typewriter tick, codex chime, trust-up, secret-unlock) | ✅ Full |

---

## UI (UI Toolkit — UXML/USS/C#)

| Screen | Status |
|--------|--------|
| **PlayerHUD** — battery bar, inventory hotbar | ✅ Full |
| **Dialogue Panel** — portraits (player left, NPC right), typewriter, suggestions, trust bar, chat history | ✅ Full |
| **NpcInfoHud** — debug panel (name, trust, topics, knowledge chips, memory stats) | ✅ Full |
| **LlmDebugWindow** (F9) — pipeline stages, system prompt, raw response, parsed envelope | ✅ Full |
| **ScanUI** — progress bar + lore result panel (built in code) | ✅ Full |
| **ResourceHP** — health bar over resource nodes | ✅ Full |
| **Codex Panel** — knowledge journal with entries, secrets, tasks tabs | ✅ Full |
| **InteractTag** — world-space interaction prompts | ✅ Full |
| **Objective Chips** — active/completed objective indicators | ✅ Full |

---

## Event Architecture

`GameEventBus` singleton with typed pub/sub (29 readonly struct events, zero allocation). Three-tier communication:

1. **GameEventBus** — primary decoupled channel (player, tool, resource, inventory, battery, terrain, rope, power, scan, transmitter, tutorial, NPC events)
2. **SO channels** — Inspector-wired (e.g. `ResourceHitChannel` for HUD)
3. **C# events + UnityEvents** — component-level (e.g. `ResourceNode.OnDamaged`)

---

## LLM Configuration

| Setting | Value |
|---------|-------|
| Provider | OpenRouter |
| Model | `meta-llama/llama-4-scout:free` or `deepseek/deepseek-chat-v3.1:free` |
| Proxy | `http://127.0.0.1:10808` |
| API Key | EditorPrefs (`Flynn.OpenRouter.ApiKey`) or env `OPENROUTER_API_KEY` |
| Embedding | Ollama `all-minilm:latest` (384-dim) at `localhost:11434` |
| Fallback | JSON-parsed content when LLM/embeddings unavailable |

---

## Items (6 defined)

| Item | Type | Auto-collect | Notes |
|------|------|-------------|-------|
| Wrench | Tool | No | Slot 0, always present |
| Biomass | Resource | Yes | Feeds processing stations (8 power/unit) |
| Wood | Resource | Yes | From trees |
| Stone | Resource | Yes | From stone nodes |
| Metal Scrap | Resource | Yes | From tech trash nodes |
| Echo Shard | Currency | Yes | Routes to `EchoShardCount` IntVariable |

---

## What's Missing / Not Yet Implemented

| Feature | Status |
|---------|--------|
| Jump mechanics | ❌ Stub — `PlayerJumpController` has no jump logic |
| Combat system (wrench swing/throw as combat) | ❌ `Player/Combat/` folder empty |
| Terrain effect zones (mud, ice, wind, low-grip) | ❌ Not in codebase despite RUNTIME_FLOWS doc |
| Audio clips | ❌ All 10 SFX profiles are empty placeholders |
| Swim/Carry/Grapple animation states | ⚠️ Never triggered by gameplay code |
| Wrench throw full mechanics | ⚠️ Scaffolded but not wired |
| Grabbable/carry system | ⚠️ State only, no carry movement |
| Solar charging (battery recharge) | 🔮 `AddCharge` exists, no source wired |
| Wind chimes dynamic audio | 🔮 Defined in content, no runtime audio system |
| Tutorial on-screen hints | ⚠️ `TutorialSignalHandler` only logs |
| Windroot Hamlet island | 🔮 Story bible + JSON complete, not in scene |
| PlayerDialogueProfile assignment | ❌ `SceneLlmManager.playerProfile` is null |
| Outline shader clipping fix | ❌ Known issue for atlas-sliced sprites |

---

## Environment Movement & Puzzle Systems (In Project — Not in Active Scene)

A separate `PuzzleSandbox.unity` scene exists for prototyping environment puzzles and movement mechanics. These systems are built but not wired into the First Light tutorial scene.

### Pushable Crates

| Feature | Status |
|---------|--------|
| `PushableCrate2D` — physics-driven pushable block (gravityScale=0, frozen rotation, continuous collision) | ✅ Full (config component) |
| Acts as windbreak — blocks `TerrainEffectZone2D` wind force when between player and wind source | ✅ Full (designed, not in active scene) |
| Crate types in PuzzleSandbox: `Crate_0`, `Crate_1`, `Crate_2`, `Crate_Pullable` | ✅ Prefabs exist |

### Wind Zones (Terrain Effect)

| Feature | Status |
|---------|--------|
| `Zone_Wind` + `FX_Zone_Wind` — wind force areas that push the player | ✅ Exists in PuzzleSandbox |
| `Tile_Wind` — visual wind tiles | ✅ Exists in PuzzleSandbox |
| Wind shadow mechanic (crate as windbreak) | ✅ Designed in RUNTIME_FLOWS |
| `TerrainStateAggregator2D` composable zone system | ⚠️ Designed in RUNTIME_FLOWS, code not in `Scripts/` |

### Elevation Puzzle Pieces

| Feature | Status |
|---------|--------|
| `Plateau` / `Group_Plateau` — elevated walkable platforms | ✅ Exists in PuzzleSandbox |
| `PlateauRamp` — smooth ramp between ground and plateau (uses `ElevationRamp`) | ✅ Exists in PuzzleSandbox |
| Walk under / stand on with occlusion fade (`ElevationZone`) | ✅ Full |
| `LandingResolver` — validates jump landings on elevated surfaces | ✅ Full (consumer is stub) |

### Planned Puzzle Mechanics (MVP Scope)

| Feature | Status |
|---------|--------|
| Push crates onto pressure plates to trigger doors/relays | 🔮 Designed — crate physics + Interactable system exist, no pressure plate logic yet |
| Use crates as windbreaks to cross wind zones | 🔮 Designed — crate + wind zone + wind shadow documented in RUNTIME_FLOWS |
| Jump across gaps between plateaus | 🔮 Designed — `LandingResolver` ready, jump controller is stub |
| Stack elevation tiers for vertical traversal | ✅ `ElevationZone` + `ElevationRamp` support multi-tier |

---

## Underground Mining (In Design — MVP Scope)

Underground mine areas where the player delves for raw resources (stone, metal scrap, tech trash) using the pick tool. Not yet built in any scene.

### Design Context

- **Tool types already defined**: `ToolType.Pick` exists in the enum alongside Wrench, Axe, Hammer — no pick item or prefab exists yet.
- **Resource types already defined**: `ResourceType.Stone`, `ResourceType.TechTrash` exist with configs and drop prefabs — currently surface nodes only.
- **Windroot Hamlet bible** references a "north cave (the Hollow)" — a memory-archive cave system. This establishes the narrative precedent for underground areas.
- **First Light bible** notes the transmitter "goes deep" when trying too hard to remember — thematically links underground = surfacing buried things.

### Planned Mining Mechanics (MVP Scope)

| Feature | Status |
|---------|--------|
| Mine entrance → transition to underground tilemap | 🔮 Planned — no scene or transition code |
| Destructible rock nodes (pick tool, `ToolEffectivenessTable` supports it) | 🔮 Planned — `ToolType.Pick` defined, no pick item/prefab |
| Resource drops: Stone, Metal Scrap, Tech Trash | ✅ Item definitions + drop prefabs exist |
| Elevation tier system for cave depth | ✅ `PlayerHeightState` + `ElevationZone` ready |
| Cave as narrative space (memory echoes, lore) | 🔮 Designed in Windroot Hamlet bible (the Hollow) |

---

## Simple Jump Traversal (In Project — Not in Active Scene)

A lightweight jump for crossing small gaps and reaching elevated platforms. The infrastructure is complete; the controller is a stub.

### Jump Infrastructure (Ready)

| Feature | Status |
|---------|--------|
| `PlayerHeightState` — manages visual Y-lift + discrete height levels | ✅ Full |
| `JumpOffset` property on `PlayerHeightState` — transient arc offset | ✅ Full |
| `ElevationZone` — solid collider toggles by height level (walk under / stand on) | ✅ Full |
| `ElevationRamp` — smooth tier transitions | ✅ Full |
| `LandingResolver` — validates landing cell against walkable colliders + elevation zones | ✅ Full |
| `PlayerJumpController` — ground cell detection + gizmos | ❌ Stub — no jump impulse, arc, or landing logic |
| Animator `Jump` trigger + clips (16-direction) | ✅ Exists — triggered by `PlayerController2D.PlayTrigger(Jump)` |

### Planned Jump Behavior (MVP Scope)

| Feature | Status |
|---------|--------|
| Press Space to jump in facing direction | 🔮 Designed — controller stub + input not wired |
| Arc trajectory via `JumpOffset` on `PlayerHeightState` | 🔮 Designed — property exists, not driven |
| Landing snap via `LandingResolver.TryResolveLanding()` | ✅ Ready — consumer is the stub controller |
| Cross small gaps between platforms | 🔮 Designed — PuzzleSandbox has plateau setups |
| Jump animation (16-direction blend) | ✅ Clips + animator trigger exist |

---

## MVP Demo Experience (What's Playable Today)

1. **Wake** on the shore of First Light island
2. **Move** with WASD across the isometric 2D world
3. **Chop overgrowth** with LMB wrench swing — full hit feedback (slash arc, hit burst, camera shake, hit-stop, debris, sprite flash, knockback)
4. **Collect biomass** — auto-flies to player, enters inventory
5. **Feed processing stations** (R key) — glow flash, transmitter gains power
6. **Watch transmitter power up** — flickering glow, venture gate opens
7. **Clean solar collectors** (interact) — dim grey → gold, steady power flows
8. **Talk to the Transmitter NPC** (E) — full LLM dialogue with typewriter, portraits, trust bar, suggestions, semantic memory recall, signal-fired story beats, codex entries
9. **Build trust** — unlock secrets at thresholds 40/50/60/70/75, milestone toasts + sounds
10. **Scan the weather station** (hold F) — progress bar, lore lines, battery drain
11. **Activate signal relay** — requires power ≥ 80 + all solar cleaned; fires tutorial-complete story beat
12. **Debug** with F9 (LLM pipeline inspector) and NpcInfoHud (NPC state panel)
