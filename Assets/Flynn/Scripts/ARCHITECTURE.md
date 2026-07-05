# Flynn — Code Architecture & Ownership

**Read this first.** It maps every `Scripts/` folder to *who owns what*.
The game is **flat 2D, top-down isometric**: gameplay is on the XY plane (player
`Rigidbody2D` gravityScale=0, `_isoRatio` 2:1 diagonal scaling); the ~30° camera tilt is
cosmetic. Contextual jumps use an `ElevationMap`, not platformer gravity.

## Golden rules

- **Managers are the control points.** Each system has ONE manager (a MonoBehaviour on the
  `MANAGERS` object) that owns its logic. Small objects stay dumb and register with their
  manager. Pattern model: `Managers/FeedbackManager.cs` (subscribes to events, drives FX).
- **Talk through the event bus, not direct references.** `Core/GameEventBus.cs`
  (`GameEventBus.Instance.Publish/Subscribe<T>`). Event structs in `Core/Events/`.
- **Namespaces:** code lives under `Flynn.*` (`Flynn.Player`, `Flynn.Terrain`, `Flynn.Map`,
  `Flynn.Npc.*`, `Flynn.Events`).
- **Assembly:** Flynn compiles into `Flynn.Runtime` (`Assets/Flynn/Flynn.Runtime.asmdef`,
  references `David.Runtime` + LiteDB). Tests in `Flynn.Tests` (`Assets/Flynn/Tests/`).
- **Moving files is safe** (`.meta` carries the GUID). **Don't delete code without asking** —
  if something is superseded, confirm before removing.

## Folder map (who owns what — verified 2026-06-23)

| Folder | Responsible for | Key control point(s) |
|---|---|---|
| `Core/` | Cross-system plumbing: event bus, event structs, tool enums/contexts | `GameEventBus`, `Events/`, `ToolType`, `ToolHitContext` |
| `Common/` | Shared SO variables, channels, anchors, sprite sorting | `IntVariable`, `PlayerAnchor`, `ResourceHitChannel`, `SpriteSortingManager` + `SortableSprite` |
| `Map/` | Tilemap layer stack, sorting per layer | `MapLayerManager`, `MapLayer` enum, `FlynnTile` |
| `Player/Movement/` | Top-down-iso movement + Animator hub + contextual jump | `PlayerController2D`, `PlayerJumpController` |
| `Player/Combat/` | Wrench swing/throw + power buildup | `WrenchSwingController`, `WrenchThrowController`, `PowerBuildupManager`, `ThrownWrench2D` |
| `Player/Interaction/` | Pickup, drop, scan, cursor/aim, interact prompts, carry | `WorldItemPickup`, `PlayerScanController`, `PlayerDropController`, `MouseCursor`, `Grabbable`/`HeldItemSocket` |
| `Player/Inventory/` | Slots, item defs, stacking | `PlayerInventory`, `InventoryData`, `ItemDefinition` |
| `Player/Power/` | Robot battery resource | `RobotBattery`, `BatterySettings` |
| `Player/Setup/` | Player wiring on spawn | `PlayerInitializer`, `PlayerAnchorRegistrar` |
| `Resources/` | Resource nodes, tool effectiveness, drops, echo shards | `ResourceManager`, `ResourceNode`, `ToolEffectivenessTable` |
| `World/` | The one droppable+pickupable item system | `WorldItem`, `WorldItemSpawner`, `DroppedItemMagnet` |
| `Terrain/` | 2D terrain effect zones (ice/wind/mud/water) | `TerrainStateAggregator2D`, `TerrainEffectZone2D`, `TerrainState2D`, `ElevationMap` |
| `Transmitter/` | **MVP objective:** power loop that gates the exit | `TransmitterStation`, `TransmitterGate`, `TransmitterFuelTable` (SO) |
| `Tutorial/` | Soft-tutorial beat sequencer, event-driven | `TutorialDirector` |
| `Managers/` | Scene-wide batch managers | `FeedbackManager`, `AudioManager` |
| `Effects/` + `Feedback/` | Hit juice: flash, impact, camera shake, hit-stop | `HitImpactFX`, `HitStop`, `CameraShake`, `HitFlash` (driven by `FeedbackManager`) |
| `Interactables/` | Scan/progress interaction contracts | `IProgressInteractable`, `ScanTarget` |
| `Audio/` | Audio config (clips pending) | `AudioProfile` (+ `Managers/AudioManager`) |
| `Environment/` | Decals, water, pushable crates | `GrassDecalPlacer`, `WaterAnimator`, `PushableCrate2D` |
| `NPC/` | LLM dialogue + semantic memory (LiteDB) + island content authoring | `DialogueManager`, `NpcMemoryDatabase` (+ `NPC/Editor/`) |
| `Editor/` | Editor-only tooling | animation setup, etc. |

## The transmitter loop (`Transmitter/`)

`TransmitterStation` owns a power pool that decays each second; the player feeds accepted
resources (`TransmitterFuelTable` SO maps resource→power) to refill it. It broadcasts
`TransmitterPowerChanged` (HUD gauge), `TransmitterVentureStateChanged` (drives `TransmitterGate`
open/closed), and depletion events. Feeding consumes from `PlayerInventory`.

## Not in the codebase (despite older docs)

Earlier docs described systems that **do not exist here** — a layered platformer core
(`PlatformerController2D`, `PlatformOcclusionManager`, `CameraFollow2D`), a `Player/Grapple/`
rope lasso, `Shadow2DManager`, `MapGeneration/` (`MapLoader`, `IslandGeneratorTwo`), and a 2.5D
billboard player. Treat references to those as stale. There is also no `_Deprecated/` folder —
don't archive there; if code is superseded, confirm before deleting.

> When you add a new system, give it a folder + add a row here naming its manager.
