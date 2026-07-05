# ProjectInstructions.md — Shared LLM Instructions for Flynn

> **Single source of truth for project-level rules, architecture, and conventions.**
> Both `CODELY.md` (Codely CLI) and `CLAUDE.md` (Claude Code) point here.
> Each tool keeps its own file for tool-specific workflow rules only.

---

## Project Overview

**Flynn** is a solarpunk survival-crafting game built in Unity. Flat 2D, top-down isometric (XY plane; player `Rigidbody2D` gravityScale=0). Player explores floating islands, gathers resources with tools (wrench swing/throw), feeds a power transmitter to progress, and interacts with LLM-driven NPCs.

- **Unity:** 2022.3.62f3 (LTS), **URP 14.0.12**, PC target
- **Input:** Legacy Input Manager only (no new Input System)
- **Active scene:** `Assets/Flynn/2D_Lighting_Demo.unity`
- **Assembly:** `Flynn.Runtime` (`Assets/Flynn/Flynn.Runtime.asmdef`). Never edit `.csproj`/`.sln`.

---

## Code Style Philosophy

**Stupid simple.** If a beginner can't follow the logic in one read-through, it's too clever.

- **One file doing one feature well** beats 5 fragments scattered across folders. Don't split a class just to hit an arbitrary line count. If a feature fits naturally in one 300-line file, that's fine. If it's genuinely doing two things, split it — but don't over-fragment.
- **Singletons are fine.** `Instance` pattern is acceptable for managers. Don't create SO event channels, runtime sets, or abstraction layers when a direct reference or singleton does the job.
- **Plain fields over abstraction.** Use MonoBehaviour serialized fields and Inspector references. Don't force ScriptableObject indirection unless the data is genuinely shared across scenes or needs designer authoring.
- **Read top-to-bottom.** Code should read like a story: setup → update → helpers. Avoid jumping through 5 files to understand one feature.
- **Name things by purpose.** `Ground_Grass_Center` not `GameObject (3)`. `PlayerHealth` not `FloatVariable`.
- **No magic strings.** Use `const` for tags, layers, animator parameters.
- **`OnValidate` guards:** `if (!Application.isPlaying) return;` for anything touching runtime state.

---

## Known Issues & TODO

> **Verify the issue still exists before acting on it.** These may be outdated.

- AudioProfiles have no audio clips — all 10 SFX profiles are empty placeholders.
- Ground TilemapCollider2D disabled — may need CompositeCollider2D.
- Tree_Resource prefabs use 3D physics — `HitBox2D` child GameObjects added as 2D physics bridge.
- Animation WIP: Swim/Carry/Grapple/Throw states never triggered by gameplay code. Only `Jump` works.
- `Visual.SpriteRenderer.flipX` was `true` despite all-16-unique sprites — may be double-flipping.
- Combat system (wrench swing/throw) is not yet implemented — `Player/Combat/` folder exists but is empty (verified 2026-07-03).
- `RUNTIME_FLOWS.md` (repo root) is part design-target, part reality — audited 2026-07-03 with ❌ NOT IMPLEMENTED banners per section. Trust the banners.
- `MVP_GAME_SUMMARY.md` "Development Progress" section overstates — it lists wrench swing, tool effectiveness, and terrain zones as built; they are not. Pitch doc, not a code reference.
- `ToolEffectivenessTable` does not exist — `ResourceNode.Hit()` decrements HP by 1 regardless of tool.
- Terrain system (`TerrainStateAggregator2D`, `TerrainEffectZone2D`, `ElevationMap`, `Terrain/` folder) does not exist. `PlayerController2D` requires only `Rigidbody2D`.
- **Outline shader clipping:** `Flynn/SpriteOutline` shader renders sprite + outline in one pass, but the outline clips at the sprite quad boundary for atlas-sliced sprites. Vertex expansion approach didn't work due to UV mapping. Future fix needed (possibly custom mesh or separate expanded render pass).
- **DB hydrate race condition:** `SceneLlmManager.SeedThenHydrate` sometimes fails with "engine already disposed" when exiting Play Mode mid-coroutine. Falls back to JSON-parsed content gracefully — not blocking.
- **TextField exception:** Unity 2022.3 UI Toolkit `ArgumentOutOfRangeException` in dialogue input field during rapid focus changes. Known Unity bug, cosmetic only.
- **`PlayerDialogueProfile` not assigned:** `SceneLlmManager.playerProfile` is null — player portrait won't show and LLM won't know player name. Need to create a SO asset and assign it.
- **`ResourceNodeConfig` for Overgrowth has no `worldPrefab`:** Biomass drops work (auto-collect) but the `ItemDefinition.worldPrefab` was only recently linked. Verify it works.
- **Biomass drop prefab** is a copy of EchoShard_Drop with green tint — needs proper art.

---

## Scope

Everything lives under `Assets/Flynn/`. Don't edit sibling subtrees (`Assets/David/`, etc.).

| Type | Path |
|------|------|
| Runtime scripts | `Assets/Flynn/Scripts/` |
| Editor scripts | `Assets/Flynn/Scripts/Editor/` or `Assets/Flynn/Editor/` |
| Configs | `Assets/Flynn/Configs/` |
| Prefabs | `Assets/Flynn/Prefabs/` |
| Materials / shaders | `Assets/Flynn/Materials/`, `Assets/Flynn/Shaders/` |
| UI | `Assets/Flynn/UI/Screens/<Name>/` (UXML + USS + C# co-located) |
| Sprites | `Assets/Flynn/Sprites/` |
| Animations | `Assets/Flynn/Animations/` |
| Scenes | `Assets/Flynn/` |

---

## Current Game State

> Verified 2026-06-24; major additions 2026-07-03. **If something here contradicts the live scene, the scene wins.** Update this file when the scene changes meaningfully.

### Added 2026-07-03 (all compiled, wired in 2D_Lighting_Demo, screencap-verified)
- **Wrench feature** (`Scripts/Player/Combat/`): WrenchController (quick/charged swing w/ .75-.9 sweetspot, RMB throw-to-cursor aim w/ freeze pose + face-lock, LMB/RMB cross-cancel), ThrownWrench (pierces resources, rebounds off solids, magnet-returns), ChargeMath + EditMode tests, WrenchChargeFX (B/W charge ring + aim dots/reticle + hit juice), PowerBuildupSettings.asset relinked. PlayerController2D refactored: swing extracted; hooks FacingOverride/HoldTrigger/AnimatorSpeedOverride/TriggerFired/ApplyKnockback.
- **Jump** (`PlayerJumpController` rewritten): Space = fixed 1-tile dash-hop (0.5u, iso Y×0.5), parabolic JumpOffset arc, LandingResolver level transitions, land squash+whoosh, wrench interlock.
- **NPC conversation mechanics**: verb chips, world-reaction signals, barks+!, topic/trust-gate strip, show+gift items — see `NPC_LLM_SYSTEM.md` §8.
- **UI pass**: PlayerHud replated (plates/action block/right rail), DialogueBox rebuilt (62% sheet, portraits, ticked trust bar, TOPICS│ITEMS strip).
- **NEXT (user-flagged, not started): codex/memory discrimination** — PlayerCodex currently captures every memory_update; needs quality gating. See NPC_LLM_SYSTEM.md §9.

- **2D on XY plane.** Player `Rigidbody2D`, gravityScale=0, camera tilt cosmetic. No billboarding.
- **Player** GO: `Rigidbody2D`, `CapsuleCollider2D`, `PlayerController2D`. Children: `VisualRoot → Visual` (SpriteRenderer + Animator), `Shadow`, `WrenchVisual`.
- **Animation:** `Flynn.controller`, 16-direction blend trees (112 clips). Hub = `PlayerController2D.UpdateAnimator()`. Params: `MoveX`/`MoveY`/`Speed`/`Carrying`/`Swimming`/`Grappling`/`Jump`/`Throw`. Clips at `Animations/16Dir/<anim>_dirNN`. dir00 = front = MoveY −1.
- **NPC dialogue system:** `DialogueManager` (singleton) opens a UI Toolkit dialogue panel, sends player input to an LLM (OpenRouter/SiliconFlow), parses JSON envelopes for replies + suggestions + memory updates + signals. No `Time.timeScale` freeze — uses `DialogueManager.IsDialogueOpen` static flag + `PlayerController2D.IsMovementLocked`. Player snaps to face the NPC via `FacePoint()`. Portraits: player on left (from `PlayerDialogueProfile.portraitSprite`), NPC on right (from `NpcAuthoringLink.portraitSprite`).
- **NPC memory:** LiteDB-backed semantic recall via `NpcMemoryDatabase` + `OllamaEmbeddingProvider` (local Ollama, `all-minilm:latest`, 384-dim). Embeds player input, brute-force cosine recall over memories + knowledge. `RecalledKnowledgeChannel` SO surfaces results to `NpcInfoHudController` for designer visibility.
- **NpcInfoHud:** Top-left debug panel showing NPC name, relationship bars, topics, fetched knowledge chips, memory stats. Auto-shows when player enters NPC range. GameObject: `MANAGERS/UI/NpcInfoHud`.
- **LlmDebugWindow:** F9 to toggle. Shows LLM pipeline stages (system prompt, chat history, raw response, parsed envelope).
- **Island content:** `IslandContentHub` loads a JSON TextAsset (currently `first_light.json`). `SceneLlmManager` seeds it into the DB on Play, embeds authored knowledge, backfills missing vectors.
- **LLM provider config:** `RemoteModelSettings` SO (`Assets/Flynn/OpenRouter/Default.asset`). Supports proxy via `proxyUrl` field. API key stored in EditorPrefs via `OpenRouterApiKey.Resolve()`.
- **Current island:** `first_light` — tutorial island. Story Bible at `PreProduction/Bibles/first_light.md`. One NPC: `npc.transmitter_station` (fragmented identity AI). 8 things, 14 signals, 33 knowledge entries.

---

## Script Folder Map

> Quick reference. Verify paths exist before relying on them.

| Folder | Key Types |
|--------|-----------|
| `Core/` | `GameEventBus`, `ToolType`, `ToolHitContext` |
| `Common/` | `IntVariable`, `SortableSprite`, `SpriteSortingManager`, `PlayerAnchor`, `Hoverable`, `HoverProfile` |
| `Player/Movement/` | `PlayerController2D`, `PlayerJumpController` |
| `Player/Elevation/` | `PlayerHeightState`, `ElevationZone`, `ElevationRamp`, `LandingResolver` |
| `Player/Interaction/` | `PlayerScanController`, `MouseCursor`, `Grabbable`, `HeldItemSocket`, `PlayerDropController`, `InteractionPrompt`, `Interactable`, `InteractionRouter` |
| `Player/Inventory/` | `PlayerInventory`, `InventoryData`, `ItemDefinition`, `InventorySlot` |
| `Player/Power/` | `RobotBattery`, `BatterySettings` |
| `Player/Setup/` | `PlayerAnchorRegistrar`, `AnimationSpeedConfigSO` |
| `Resources/` | `ResourceNode`, `ResourceNodeConfig`, `DropEntry`, `ResourceType`, `HitDebrisBurst` |
| `World/` | `WorldItem`, `WorldItemSpawner`, `DroppedItemMagnet` |
| `Transmitter/` | `TransmitterStation`, `TransmitterGate`, `TransmitterFuelTable` |
| `Map/` | `MapLayerManager`, `MapLayer`, `FlynnTile` |
| `Effects/` `Feedback/` | `HitImpactFX`, `HitStop`, `CameraShake`, `SpriteFlash` |
| `Interactables/` | `ScanTarget`, `ScanTargetConfig` |
| `NPC/` | `DialogueManager`, `NpcMemoryDatabase`, `SceneLlmManager`, `NpcInteraction`, `IslandContentHub`, `NpcAuthoringLink`, `NpcClickInteraction`, `WorldThingLink` |
| `Tutorial/` | `ProcessingStation`, `SolarCollector`, `SignalRelay`, `ScanUIController`, `TutorialSignalHandler` |
| `Environment/` | `GrassDecalPlacer`, `WaterAnimator`, `PushableCrate2D` |

### System-specific docs

Read before touching, update after changing:

| System | Doc |
|--------|-----|
| Player | `Scripts/Player/Player.md` |
| NPC | `Scripts/NPC/NPC_Memory.md` |
| World items | `Scripts/World/WorldItem.md` |

---

## Implementation Communication

After any change, provide a brief **Change Summary** (under 10 lines):

- **What changed** — files, GameObjects, components
- **Why** — what problem it solves
- **How to verify** — what to look at in the Editor
- **What to watch for** — side effects or manual adjustments needed

## Decision Log Convention

For non-obvious choices, add a `// DECISION:` comment:

```csharp
// DECISION: Used singleton instead of SO event because only one scene
// needs this and the indirection would make it harder to follow.
```

---

## Implementation Checklist

### Before
1. **Reuse before inventing.** Search Flynn first.
2. **Read the target before mutating.** Fetch current state before changing.
3. **Look at neighbours.** Copy existing scale/position/sorting as a starting point.

### During
4. **One change, one verify.** Compile → check console after each meaningful step.
5. **Match Flynn's spatial convention.** 2D on XY, URP 2D lighting, 16-direction Animator.
6. **No magic strings.** Read tags/layers/animator params from the project.

### After
7. **Console clean.** Warnings count as not done.
8. **Ask user to visually inspect.** Don't assume success from code alone.
9. **Clean AI tells.** Remove `// TODO`, placeholder comments, unused `using`, default-only fields, generic method names.

---

## Unity Quirks

- **SpriteShape clones share splines.** Replace with fresh `Spline()` before writing points (see `MapLoader` reference if it exists).
- **SpriteShape rebuild order:** `RefreshSpriteShape()` → `UpdateSpriteShapeParameters()` → `BakeMesh().Complete()` → `RefreshSpriteShape()`.
- **Hidden template SpriteShapes** in generators — don't delete, they're Inspector-assigned.
- **Generated object naming:** prefixed (`Ground_`, `Decal_`, `Resource_`, `Npc_`, `Sprite_`).
- **Never delete `.meta` without its asset** — orphans regenerate with new GUIDs.

---

## UI System

- **UI Toolkit** (UXML/USS/C#), not uGUI.
- Files at `Assets/Flynn/UI/Screens/<ScreenName>/` (co-located).
- Palette and tokens: see `Assets/Flynn/UI/Styles/tokens_reference.md` (create if missing).
- Draft aesthetic: flat black surfaces, 1px white outlines, square corners, monospace for numbers. No gradients, glow, rounded corners, or decoration.
- Dialogue UI: `Assets/UI Toolkit/DialogueBox.uxml` + `.uss`. Built in code by `DialogueManager.BuildFallbackUi()` if UXML elements are missing. Layout: player portrait (left) → conversation scroll (center) → NPC portrait (right).
- Scan UI: `ScanUIController` builds UI in code (no UXML). Progress bar on scan, result panel for lore lines. GameObject: `MANAGERS/UI/ScanUI`.
- NpcInfoHud: `Assets/Flynn/UI/Screens/NpcInfoHud/` (UXML + USS + C#).

---

## Interaction & Hover System

Standard pattern for all interactable world objects:

1. **`Hoverable`** (`Scripts/Common/Hoverable.cs`) — requires `SpriteRenderer` + `Collider2D`. On hover: swaps SpriteRenderer material to outline material (material swap, no child object). Scale pop optional. Requires a `HoverProfile` SO assigned (no fallback). Shared profile: `Assets/Flynn/Configs/UI&Effects/Standard Hover.asset` (or `Assets/Flynn/Configs/HoverProfile.asset`).
2. **`Interactable`** (`Scripts/Player/Interaction/Interactable.cs`) — serialized component with activation key, range, prompt verb/label, and `OnInteract` UnityEvent. Wire the event to any method (e.g. `ProcessingStation.TryProcess`, `SolarCollector.Clean`). Implements `IInteractionPromptProvider` for hover tag display.
3. **`InteractionRouter`** (`Scripts/Player/Interaction/InteractionRouter.cs`) — on Player. Each frame: finds `Interactable` on hovered GO (walks up hierarchy), routes activation key press to `Interact()`. Blocked during dialogue.

**Adding a new interactable:**
1. Add `Hoverable` component → assign `HoverProfile`
2. Add `Interactable` component → set key, range, prompt text in Inspector
3. In `OnInteract` UnityEvent → drag target object → select method to call
4. Done — no custom input code needed

**Objects that keep their own input handling (not via Interactable):**
- `ResourceNode` — LMB swing handled by `PlayerController2D` → `ResourceHit` event. Uses `Hoverable` + `Interactable` (prompt only, no OnInteract wired).
- `ScanTarget` — F-key hold-to-scan handled by `PlayerScanController`. Uses `Hoverable` for outline.
- `TransmitterStation` — R-key feed handled internally. Has `NpcAuthoringLink` for dialogue (E/click).

---

## Tutorial Island — First Light

**Island:** `first_light` (replaces `windroot_hamlet`). Story Bible: `PreProduction/Bibles/first_light.md`. Island JSON: `Configs/NPC/Islands/first_light.json`.

**Core loop:** Chop overgrowth → collect biomass → feed processing stations → transmitter powers up → clean solar collectors for steady power → activate signal relay.

**Scene objects (around transmitter at -1.79, 0.33):**
- `ion_TransmitterStation` — NPC (`npc.transmitter_station`), `TransmitterStation` (startPower=5, decay=0, threshold=80, playerAnchor wired, biomass in fuel table at 8 power/unit)
- `ProcessingStation_1/2` — `ProcessingStation` + `Interactable` + `Hoverable` + `WorldThingLink`
- `Overgrowth_1-5` — `ResourceNode` (Overgrowth_Config, 2HP, drops biomass) + `Hoverable` + `Interactable`
- `SolarCollector_1-3` — `SolarCollector` + `Interactable` + `Hoverable` + `WorldThingLink` (dim grey → gold on clean, steady power)
- `SignalRelay` — `SignalRelay` + `Interactable` + `Hoverable` + `WorldThingLink` (requires 80 power + all 3 solar cleaned)
- `WeatherStation` — `ScanTarget` (3s scan, lore lines) + `Hoverable` + `WorldThingLink`
- `WindChimes` — decorative, `WorldThingLink` only
- `Shore_Marker` — decorative, `WorldThingLink` only
- `TutorialSignalHandler` — on MANAGERS, listens for dialogue signals (tutorial.first_feed, etc.)

**Assets created:**
- `Configs/Items/Biomass_Item.asset` — autoCollect, maxStack 99, worldPrefab=Biomass_Drop
- `Prefabs/Items/Biomass_Drop.prefab` — copy of EchoShard_Drop, green tint, WorldItem→Biomass
- `Configs/Resources/Overgrowth_Config.asset` — 2HP, drops 1-2 biomass, wrench tool
- `Configs/ScanTargets/WeatherStation_Config.asset` — 3s scan, island info lines
- `Configs/UI&Effects/Standard Hover.asset` — HoverProfile SO (outline mat, 1.05 scale, 12 lerp)
- `Configs/HoverProfile.asset` — older profile, also exists

**Hit feedback system:**
- `HitDebrisBurst` (`Scripts/Resources/HitDebrisBurst.cs`) — on `ResourceHit` event, spawns debris sprites with random velocity, gravity, spin, fade. Configured on: Metal_Scrap (env_015-020), Tree (env_021-035), Stone (env_001-008).
- `ResourceNode` cross-fade stage transitions — `UpdateStageSprite` now creates a temporary ghost SpriteRenderer for the old sprite, fades it out while fading in the new one (0.2s `_stageFadeDuration`). Flash + scale pop still fire on top.
- `ResourceNode` `RefreshPolygonCollider` preserves `isTrigger` when recreating collider after stage sprite swap.

**LLM config:**
- Provider: OpenRouter, model: `meta-llama/llama-4-scout:free` (or `deepseek/deepseek-chat-v3.1:free`)
- Proxy: `http://127.0.0.1:10808` (configurable on `RemoteModelSettings.proxyUrl`)
- API key: EditorPrefs (`Flynn.OpenRouter.ApiKey`) or env var `OPENROUTER_API_KEY`
- Embedding: Ollama `all-minilm:latest` (384-dim) at `http://127.0.0.1:11434/api/embeddings`

---

## Troubleshooting

After **3 failed attempts**, stop and ask the user — they may see something obvious.
