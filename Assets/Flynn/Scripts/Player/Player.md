# Player System

**Code:** `Assets/Flynn/Scripts/Player/`
**Prefab:** `Assets/Flynn/Prefabs/Player.prefab` — hand-placed persistent scene instance (not runtime-spawned).

## Genre / format
**Flat 2D, top-down isometric.** Gameplay is on the XY plane; the ~30° camera tilt is cosmetic.
The player `Rigidbody2D` runs `gravityScale = 0`. Facing is conveyed by a **16-direction sprite
Animator** (no transform rotation, no billboarding). Legacy `Input` manager
(`Input.GetAxisRaw`, `GetButtonDown`, `GetMouseButtonDown`).

> History: this replaced an earlier 2.5D billboard prototype (`SolarpunkCharacterController`,
> `FlynnAnimationDriver`, `PlayerMouseAimer`, rope lasso) — all of which are **gone from the
> codebase**. Don't reference them.

## Components

### Movement (`Player/Movement/`)
| Script | Role |
|---|---|
| `PlayerController2D` | The movement + Animator hub. `[DefaultExecutionOrder(-50)]`, `[RequireComponent(Rigidbody2D, TerrainStateAggregator2D)]`. Reads `Horizontal`/`Vertical`, scales Y by `_isoRatio` (0.5) on diagonals to align with the iso grid, accelerates toward a terrain-modified target velocity in `FixedUpdate`. Exposes `MoveInput`/`LastMoveDirection`/`Velocity`/`NormalizedSpeed`/`IsMoving`/`IsMovementLocked`/`CanJump`/`HeightIndex`. Owns the `_animationState` (`PlayerAnimationState` enum) and `UpdateAnimator()`. |
| `PlayerJumpController` | Contextual isometric jump using `ElevationMap`. Locks `PlayerController2D.IsMovementLocked` during the arc, calls `PlayTrigger(Jump, duration)`. The only state currently driven by gameplay. |

### Animation hub — `PlayerController2D.UpdateAnimator()`
Drives Animator `Assets/Flynn/Animations/Flynn.controller` (8 params). Each anim is a
**16-direction 2D Freeform Cartesian** blend tree on `MoveX`/`MoveY` (112 clips at
`Animations/16Dir/<anim>_dirNN`; dir00 = facing camera = MoveY −1, `az=(270+22.5·dir)%360`).
- `MoveX`/`MoveY` — smoothed `_lastMoveDirection` (lerp by `_directionSmoothing`).
- `Speed` — `Clamp01(NormalizedSpeed)`; blends idle↔run in the 1D `Locomotion` tree.
- `Swimming`/`Carrying`/`Grappling` bools — set from `_animationState` (Swim/Carry/Grapple).
- `Jump`/`Throw` triggers — `PlayTrigger(state, targetDuration)` scales `animator.speed` so the
  clip finishes in `targetDuration`, fires the trigger once, then falls back to Idle.

### Combat (`Player/Combat/`)
| Script | Role |
|---|---|
| `WrenchSwingController` | LMB melee swing. Charges via `PowerBuildupManager`, emits a `ToolHitContext` to `ResourceNode`/`HittableSurface`. |
| `WrenchThrowController` | RMB throw. Charges, spawns `ThrownWrench2D` boomerang. **Does NOT yet fire the `Throw` animation** (gap). |
| `PowerBuildupManager` | Shared charge ramp 0→1 (~2s); perfect-zone 75-90% = bonus damage. Used by swing + throw. |
| `ThrownWrench2D` | Boomerang projectile: out → hover → return → caught. |
| `WrenchSwingArc`, `WrenchVisual`, `WrenchConfig`, `HittableSurface`, `PowerBuildupSettings` | Swing arc FX, held-wrench visual, tunable SOs, non-resource hittable tagging. |

### Interaction (`Player/Interaction/`)
| Script | Role |
|---|---|
| `MouseCursor` | Custom crosshair cursor + aim line. |
| `PlayerScanController` | Hold-to-scan `IProgressInteractable` targets; drains battery. |
| `WorldItemPickup` | **DEPRECATED, NOT a player component.** A marker on pickup-able world objects (`[RequireComponent(Collider)]`, holds an `ItemDefinition`); its docstring references the removed `PlayerMouseAimer`. Pickup is now handled by `WorldItem` (see `World/WorldItem.md`). Do not add it to the Player. |
| `PlayerDropController` | G drops one unit of the active slot; HUD drag-off drops a stack. |
| `InteractionPrompt` | World-anchored `[E]/[Q]` prompt contract for hovered interactables. |
| `Grabbable`, `HeldItemSocket` | Carry system: grabbable objects + the socket they attach to. |
| `MouseHoverDebugGizmos` | Editor hover debug. |

### Inventory / Power / Setup
| Script | Role |
|---|---|
| `Inventory/PlayerInventory` | Singleton-ish runtime `InventorySlot[]` from `InventoryData`. Slot 0 = wrench; Alpha1-4 switch. Stacking to `ItemDefinition.maxStack`. Currency bypasses slots. |
| `Inventory/InventoryData`,`InventorySlot`,`ItemDefinition`,`ItemType` | Data SOs + enums. |
| `Power/RobotBattery` | `[DefaultExecutionOrder(-40)]`. 0-100 charge, passive drain + action costs (swing2/throw5/grapple8/scan5ps/pull3ps). Publishes BatteryChanged/Low/Empty events. |
| `Power/BatterySettings` | Tunable SO. |
| `Setup/PlayerInitializer`, `PlayerAnchorRegistrar` | Spawn wiring; registers player Transform into a `PlayerAnchor` SO (no `FindObjectOfType`). |
| `AnimationSpeedConfigSO`, `IPlayerVisual` | Per-state anim-speed config; visual interface. |

## Facing / flip note (gotcha)
`PlayerController2D` ALSO computes a legacy 3-way `FacingDirection` (Down/Left/Up) and, when
`_flipToFaceDirection == true`, sets `SpriteRenderer.flipX` for leftward motion. This predates
the all-16-unique sprites and **double-flips** facing if left on. The live Player has
`_flipToFaceDirection = false` (correct). If you see flipX flipping the sprite, that's why.

## Gaps / TODO
- **⚠ `Player.prefab` was missing 4 player combat/interaction components** (verified 2026-06-23, likely
  lost in the recent "mega upload" churn): `WrenchSwingController`, `PlayerScanController`,
  `PlayerDropController`, `WrenchThrowController`. (`WorldItemPickup` is NOT one of these — it's a
  deprecated world-object marker, see table above.) The **scene instance** in `2D_Lighting_Demo` was
  patched with all 4 + a new `WorldInteractTagPresenter` (presenter lives on `MANAGERS/UI/InteractTag`,
  not the Player). Check the receipt for whether the 4 were also pushed down to `Player.prefab` — if not,
  new Player instances still spawn without them.
- **Hover interact tags now work**: `WorldInteractTagPresenter` (`UI/Screens/InteractTag/`) bridges
  `MousePointer.OnHoverChanged` → `IInteractionPromptProvider.TryGetPrompt` → `InteractTagPanel`.
  Previously the panel existed but nothing drove it.
- **Swim / Carry / Grapple / Throw never play.** `UpdateAnimator` sets their bools/trigger from
  `_animationState`, but no gameplay code sets `_animationState` to those values. Only `Jump` is
  driven (by `PlayerJumpController`). Throw mechanic runs but never fires the `Throw` trigger.
- Tool gating not enforced beyond "wrench is active slot".
- World/resource art is placeholder; player hand-placed (not data-driven spawn).
- No grapple/rope system in code (the old `RopeLasso*` was removed, not ported to 2D).

---
**LLM: after changing the player, update this file** (components, current state, gaps). Keep it short and accurate.
