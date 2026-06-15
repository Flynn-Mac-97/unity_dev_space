# Flynn — Code Architecture & Ownership

**Read this first.** It maps every Scripts/ folder to *who is responsible for what*.
The game is a **layered 2.5D platformer**: gameplay is real 2D (XY + gravity); the
~30° camera tilt is cosmetic. See the MVP plan for the full design.

## Golden rules

- **Managers are the control points.** Each system has ONE manager (a MonoBehaviour
  on the `MANAGERS` object) that owns its logic. Many small objects stay dumb and
  register with their manager, which does the work in one batch loop. Pattern model:
  `Managers/Shadow2DManager.cs` + `Common/Shadow2DTarget.cs`.
- **Talk through the event bus, not direct references.** `Core/GameEventBus.cs`
  (`GameEventBus.Instance.Publish/Subscribe<T>`). Event structs in `Core/Events/`.
- **Namespaces:** new code lives under `Flynn.*` (`Flynn.Platforming`, `Flynn.Map`,
  `Flynn.Npc.*`, `Flynn.Events`). Older files are being migrated incrementally.
- **Moving files is safe** (single assembly, no asmdef; `.meta` carries the GUID).
  Deleting is not — archive to `_Deprecated/` instead.

## Folder map (who owns what)

| Folder | Responsible for | Key control point(s) |
|---|---|---|
| `Core/` | Cross-system plumbing: event bus, event structs, tool enums/contexts | `GameEventBus`, `Events/GameEvents` |
| `Platforming/` | **Movement & the layered view.** 2D platformer physics, jump-height shadow cue, platform occlusion fade | `PlatformerController2D` (player), `PlatformOcclusionManager` (batch) |
| `Map/` | Tilemap layer stack (background→ground→platforms→props→foreground) | `MapLayerManager`, `MapLayer` enum |
| `Transmitter/` | **MVP objective:** start-zone power loop — decays, fed by resources, gates the exit | `TransmitterStation` (owns power), `TransmitterGate`, `TransmitterFuelTable` (SO) |
| `Tutorial/` | Soft-tutorial beat sequencer (wrench→gather→scan→power), event-driven | `TutorialDirector` |
| `Managers/` | Scene-wide batch managers | `Shadow2DManager`, `FeedbackManager`, `AudioManager` |
| `Player/Movement/` | Legacy/alt controllers, lean visuals | `PlayerController2D` (top-down 2D), `VelocityLeanDriver` |
| `Player/Combat/` | Wrench swing/throw + power buildup | `WrenchSwingController`, `WrenchThrowController`, `PowerBuildupManager` |
| `Player/Inventory/` | Held items, slots, item defs | `PlayerInventory`, `InventoryData`, `ItemDefinition` |
| `Player/Interaction/` | Pickup, drop, aim, scan, interact prompts, carry | `PlayerPickupController2D`, `PlayerScanController`, `PlayerMouseAimer2D` |
| `Player/Grapple/` | Rope grapple (same-plane pull + object pull) | `RopeLassoController`, `RopeAnchor`, `RopePullable` |
| `Player/Power/` | Robot battery / power resource | `RobotBattery`, `BatterySettings` |
| `Player/Setup/` | Player wiring on spawn | `PlayerInitializer`, `PlayerAnchorRegistrar` |
| `Resources/` | Resource nodes, tool effectiveness, drops, echo shards | `ResourceManager`, `ResourceNode`, `ToolEffectivenessTable` |
| `World/` | The one droppable+pickupable item system | `WorldItem`, `WorldItemSpawner`, `DroppedItemMagnet` |
| `Terrain/` | 2D terrain effect zones (ice/wind/mud/water) | `TerrainStateAggregator2D`, `TerrainEffectZone2D`, `TerrainState2D` |
| `Effects/` + `Feedback/` | Hit juice: flash, impact, camera shake, hit-stop | `FeedbackManager` (subscribes to events) |
| `NPC/` | LLM dialogue + semantic memory (LiteDB) + island content authoring | `DialogueManager`, `SceneLlmManager`, `NpcMemoryDatabase` |
| `Interactables/` | Scan/progress interaction contracts | `IProgressInteractable`, `ScanTarget` |
| `Audio/` | Audio config (clips pending) | `AudioProfile` (+ `Managers/AudioManager`) |
| `Environment/` | Decals, water, pushable crates | `GrassDecalPlacer`, `WaterAnimator`, `PushableCrate2D` |
| `MapGeneration/` | JSON→world build, procedural island helpers | `MapLoader`, `IslandGeneratorTwo` |
| `Common/` | Shared SO variables, channels, anchors, shadow targets | `IntVariable`, `Shadow2DTarget`, `ResourceHitChannel` |
| `Editor/` + `NPC/Editor/` | Editor-only tooling | community editor, memory browser, settings windows |
| `_Deprecated/` | Superseded 2.5D / top-down code — kept for reference, not used | see below |

## The layered-platformer core (`Platforming/`)

One controller + two managers cover the whole "jump in a tilted view" design:

- **`PlatformerController2D`** (on the Player). 2D gravity + jump + a single downward
  raycast that yields three outputs everything else reads:
  `IsGrounded`, `HeightAboveSurface` (feet→ground gap), `CurrentPlatform`.
- **Drop-shadow** reuses `Shadow2DManager`: the controller feeds
  `HeightAboveSurface` into `Shadow2DTarget.DynamicLift`, so the shadow stays on the
  ground and the feet→shadow gap *is* the jump height (== the fall distance off a ledge).
- **`PlatformOcclusionManager`** (batch): fades any `OccludablePlatform` the player
  passes under, so a higher platform never hides the player on the tilted camera.
- **`CameraFollow2D`**: smooth 2D follow on a fixed-tilt camera rig.

## The transmitter loop (`Transmitter/`)

`TransmitterStation` owns a power pool that decays each second; the player feeds
accepted resources (`TransmitterFuelTable` SO maps resource→power) to refill it.
It broadcasts `TransmitterPowerChanged` (HUD gauge), `TransmitterVentureStateChanged`
(drives `TransmitterGate` open/closed), and `TransmitterDepleted` (lockout/NPC warning).
Feeding consumes from inventory via `PlayerInventory.TryConsume`.

## `_Deprecated/` — archived on the 2D-side-view switch

Superseded by a 2D replacement (kept only for reference):
`Movement_2_5D/` (SolarpunkCharacterController, ProceduralCharacterDriver),
`Animation_2_5D/` (FlynnAnimationDriver), `Billboard/` (Billboard*, BillboardManager),
`Interaction_TopDown/` (PlayerMouseAimer, PlayerPickupController, WorldInteractTagPresenter),
`Terrain_TopDown/` (TerrainEffectZone, TerrainState, TerrainStateAggregator),
`Shadow_Legacy/` (ShadowManager), `MapGen_Drafts/` (IsleGenerator).

> When you add a new system, give it a folder + add a row here naming its manager.
