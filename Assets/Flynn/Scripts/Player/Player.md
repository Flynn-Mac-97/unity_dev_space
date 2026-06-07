# Player System

**Code:** `Assets/Flynn/Scripts/Player/`
**Prefab:** `Assets/Flynn/Prefabs/Player.prefab` — feature-complete. Used as a **persistent scene instance**, not runtime-spawned (MapLoader spawn is OFF; see `MapGeneration/MapLoader.md`).

## Genre / format (important)
2.5D: a **3D world with 3D physics** (movement on the XZ plane, Y up), but every sprite is a **billboard that always faces the camera**. Consequences:
- The player's facing is the billboard sprite, **not** a real transform rotation. Aim direction (mouse → world) and the sprite's visual facing are decoupled — pick facing from the aim/move vector, don't read transform.forward.
- **Any world sprite (projectiles, pickups, FX) should billboard too**, or it looks tilted. The thrown wrench billboards in code; the pickup uses the `Billboard` component.
- Legacy `Input` manager.

## Components (all on Player root)
| Script | Role |
|---|---|
| `SolarpunkCharacterController` | 4-dir **force-based** move (accel-to-target via `AddForce`, baseline drag 0 so accel-to-target also arrests motion and jump/fall aren't damped) + impulse jump on 3D Rigidbody; raycast ground check. This replaced the old direct-`rb.velocity` model so built-in physics composes: `PhysicMaterial` friction, `Rigidbody.drag` (water zones), and `AddForce` (wind zones) all work. Exposes `MoveInput`/`IsGrounded`/`NormalizedSpeed`, and terrain modifiers `SteeringControl` (ice → low steering force, momentum carries, never 0 so escapable), `SpeedMultiplier` (water/mud → lower target speed = noticeable slow), `CanJump` (mud → blocks jump); all auto-reset each FixedUpdate and re-asserted by `TerrainEffectZone`. |
| `FlynnAnimationDriver` | Controller → Animator. **Locomotion is a velocity blend tree**: feeds `VelocityX/Y/Z` (= `RbVelocityDirection`) + `Speed` (XZ magnitude) + `IsGrounded`; the tree owns direction/clip selection (no more `FacingDir` octants, no sprite flip, no per-direction `animator.speed` multipliers — all stripped). Owns `animator.speed` only for the **swing phase** API: `BeginChargePose(idx)` starts the windup (clip paused), `UpdateChargePose(idx, charge)` scrubs the paused clip across windup frames 1→5 as charge builds (holds at 5) and live-swaps the tool, `ReleaseSwing(idx, speed)` resumes from the held windup frame at a charge-scaled speed (heavier = slower), `CancelSwing()` aborts to idle; locomotion resets `animator.speed = 1` otherwise. Also `TriggerAttack(idx)` (legacy AnyState trigger), `IsAttacking` (attack states tagged `Attack`). `FaceWorldDirection(dir)` is now a **no-op** kept for `IPlayerVisual` (facing comes from velocity). |
| `PlayerMouseAimer` | Owns all mouse→world target detection (read-only). `WorldAimPoint` + per-frame raycasts (one shared `RaycastForComponent<T>` helper, triggers ignored, `GetComponentInParent<T>()`): `HoveredResource`/`HoveredPickup` (range-gated by `_interactionRange`), `HoveredAnchor`/`HoveredPullable`/`HoveredGrapplePoint` (rope target + exact hit point, **not** range-gated — controller owns range). **Swing target** (gated by `_meleeRange` on `_meleeLayers`): `SwingAnimIndex` (tool of the surface under the cursor — `ResourceNode` or `HittableSurface`; defaults 4=wrench/air) and `MeleeResource` (the node to damage, null for non-resource surfaces). Per-concern `LayerMask`s: `_aimLayers`/`_resourceLayers`/`_pickupLayers`/`_grappleLayers`/`_meleeLayers`. |
| `PlayerInventory` | **Stacking, data-driven slot count.** Runtime `InventorySlot[]` (item + count) copied from `InventoryData` on Awake. Slot 0 = wrench; slots 1..N = general (start 3, `InventoryData.resourceSlotCount`, extendable). Each slot stacks one item kind up to `ItemDefinition.maxStack` (3). `TryAddItem(item,count)` merges into matching stacks then fills empty slots, returns count added; `HasRoomFor`, `IsFull`, `RemoveOne`/`RemoveStack`/`RemoveFromSlot`, `SlotCount`. Currency items never enter slots. Hotkeys 1–4 select active slot. Events for the HUD. |
| `PlayerPickupController` | **E** + aim at a **key-pickup `WorldItem`** (`autoCollect == false`, e.g. the wrench) → `TryAddItem`, destroy/reduce. Auto-collect drops fly in on their own (see `DroppedItemMagnet`). Reads `PlayerMouseAimer.HoveredKeyPickup`. |
| `PlayerDropController` | **G** drops one unit of the active slot; HUD drag-off-the-bar calls `DropSlot(index)` to drop a whole stack. Spawns via `WorldItemSpawner` with a re-collect delay so dropped resources don't instantly magnet back. |
| `PlayerAnchorRegistrar` | Registers the player Transform into a `PlayerAnchor` SO so runtime-spawned drops home in on the player without `FindObjectOfType`. |
| `WrenchSwingController` | **LMB**: hold to charge, **swing fires on release**. During the hold the matching tool **winds up across frames 1→5** (scrubbed by charge, paused at 5) and live-swaps with the surface under the cursor; on release the swing resumes from the held frame at a charge-scaled speed (tap = fast light swing, full charge = slower heavy swing + longer cooldown). Tool/target read from `_aimer.SwingAnimIndex`/`MeleeResource` (air→wrench, wood→axe, metal→hammer, rock→pick). Harvest damage lands only on `ResourceNode`s. Cancels the windup if the wrench is unequipped or thrown mid-charge. |
| `WrenchThrowController` | **RMB**: tap = short throw, hold = charge → farther. Spawns `ThrownWrench`. One airborne at a time. |
| `RopeLassoController` | **Q**: hold-charge, release = winch grapple. Grapples exactly what the cursor is over (reads aimer's `HoveredAnchor`/`HoveredPullable`/`HoveredGrapplePoint`); marker kind decides mode: `RopePullable` → reel object up to player; `RopeAnchor` → reel player to the **exact clicked surface point**. Range gated here via `_config.minRange`/`maxRange` against the hit point. Force-based (`AddForce`), suppresses own steering mid-self-reel via `SteeringControl`. Blocked on mud (reads `CanJump`). `LineRenderer` rope (`RopeLine` child) shows hand→target while reeling. Exposes `IsCharging`/`ChargeNormalized`/`IsPulling`. |
| `PlayerAimReticle` | Ground arrow at the player pointing along aim; grows/recolors with charge. |

**Wrench abilities require the wrench to be the _active_ slot** (`ActiveItemType == Wrench`), not merely owned. Disabled while a throw is airborne.

## Wrench subsystem (the multitool)
- `WrenchConfig` (SO, `Configs/Player/WrenchConfig.asset`) — all swing/throw tunables (charge times, heavy threshold, cooldowns, throw distances/speeds, boomerang return delay, catch radius).
- `ThrownWrench` — projectile: outbound → hover (`returnDelay`) → boomerang home → caught (`OnCaught`). Billboards + spins in screen plane. Square placeholder sprite.

## Rope lasso subsystem (winch grapple)
- `RopeLassoConfig` (SO, `Configs/Player/RopeLassoConfig.asset`) — tunables: `chargeTime`, `minRange`/`maxRange` (~2 units/tile, both live), `pullForce`/`maxPullSpeed`/`stopRadius`/`pullTimeout`, `steeringDuringPull`.
- `RopeAnchor` — marker on a latch point (`AnchorType` Stub/Stone/Rock). No registry/trigger — a **solid collider on a grapple layer**; mouse raycast in `PlayerMouseAimer` detects it, lasso latches the exact hit point.
- `RopePullable` — marker on a movable Rigidbody object. No registry — just its **solid collider on a grapple layer** (no separate detection trigger needed).
- Marker component decides mode: `RopeAnchor` → self-winch to clicked point; `RopePullable` → object-winch (no height check). Wrench-gated like swing/throw.

## HUD (UGUI, code-built — `UI/Screens/PlayerHudUGUI.cs`)
**Single self-contained component** on the `PlayerHUD` GameObject. Builds its own Screen-Space-Overlay Canvas + all widgets in `Awake` (no UXML, no prefab, no UI-Toolkit OnEnable timing trap):
- **Hotbar** — slot count from `PlayerInventory.SlotCount` (built in `Start`), driven by inventory events: active slot white/others dim, item `icon` + **stack count** shown, **drag a slot off the bar to drop it** (`HotbarSlotDragHandler`), **FULL** tag when no slot is free.
- **Charge bar** — appears while `_swing`/`_throw` `IsCharging`; fill = `ChargeNormalized`, light/heavy tick.
- **Interaction prompt** — generic world-anchored prompt (`[E] Pick Up`, `[Q] Grapple`, future `[E] Talk`/`Inspect`) floating over the hovered target. Driven by the **interaction-prompt system**: `IInteractionPromptProvider.TryGetPrompt` returns an `InteractionPrompt` (key/verb/label/anchor); `PlayerMouseAimer.HoveredInteractable` surfaces the target via a generic `_interactLayers` raycast (+ rope-lasso fallback). The HUD renders whatever prompt comes back — knows no concrete types. Implemented by `WorldItem` (key-pickup), `RopeAnchor`/`RopePullable` (grapple). **Add a new interactable = implement the interface; no HUD/aimer change.**
- **Resource HP bar** — world-anchored above a struck node (driven by `ResourceHitChannel`); fill lerps down on each hit, fades out after a beat.
- **Echo Shard counter** — top-right (Stardew-money style), driven by the `EchoShardCount` `IntVariable`.
Inspector refs: component refs (inventory/aimer/swing/throw/dropController) auto-resolve from scene if null; **asset refs (hitChannel/echoShards/echoIcon) must be wired** or those widgets stay hidden.

**Retired** (set inactive in scene, kept for reference): the UI-Toolkit HUDs `WrenchHud` (UIDocument + `WrenchHudController` / `WrenchHudUGUI`) and `Hotbar` (UIDocument + `HotbarController`). The UXML Hotbar had a runtime-binding timing bug; replaced wholesale by UGUI.

**Wrench starts equipped**: `Configs/Player/PlayerInventory_Default.asset` slot 0 = `Wrench_Item`, so the wrench is active at spawn and swing/throw/grapple work without picking it up first.

## Data & world targets (same folder)
- `ItemType` (None/Pick/Axe/Hammer/Wrench), `ItemDefinition` SO (now also: `maxStack`, `autoCollect`, `isCurrency`+`currencyTarget`, `worldPrefab`), `InventoryData` SO (`resourceSlotCount`, slot 0 = wrench).
- **World-item / drop system — see `Scripts/World/WorldItem.md`.** `WorldItem` is the one droppable/pickupable unit; auto-collect items magnet to the player (`DroppedItemMagnet`), the wrench is key-pickup. Everything (resource drops, echo shards, inventory drops) spawns through `WorldItemSpawner`.
- `WorldItemPickup` — **DEPRECATED**, superseded by `WorldItem`; kept only so the retired UXML HUDs compile.
- `ResourceNode` — harvestable; HP + `OnDamaged`/`OnDepleted` events, raises `ResourceHitChannel` per hit, no longer self-destroys when a `ResourceDeathFall` is present. Feedback components (in `Scripts/Resources/`): `ResourceShake` (hit recoil), `ResourceDeathFall` (topple+fade+destroy), `ResourceDropSpawner` (eject drops), `EchoShardRoller` (per-hit echo-shard chance). `ResourceNodeConfig` gained `echoShardChancePerHit`/`echoShardItem`; `DropEntry` now references an `ItemDefinition` (its `worldPrefab` is the dropped object).
- `EchoShardCount` (`IntVariable` SO) — out-of-inventory currency shown top-right by the HUD. `PlayerAnchor` SO + `ResourceHitChannel` SO wire drops→player and resources→HUD without singletons.
- `HittableSurface` / `SurfaceMaterial` — tags a non-resource prop (crate, wall) as Wood/Metal/Rock so the wrench swings the matching tool (Wood→axe, Metal→hammer, Rock→pick). `AttackAnimIndex` matches `ResourceNode`'s. ResourceNodes don't need it (they derive their tool from `requiredTool`).
- `RopeAnchor` / `RopePullable` — rope-lasso targets, raycast-detected by component. See Rope lasso subsystem.
- Placeholder square sprite: `Sprites/placeholder_square.png` (used by wrench pickup, thrown wrench).

## Animator (`Flynn_AnimatorController`)
Locomotion is a **velocity blend tree** driven by `VelocityX/Y/Z` (Rigidbody velocity), with `Speed`(f) + `IsGrounded`(b) for the idle/run/air split. Attack states `Attack_{Pick,Axe,Hammer,Wrench}` (each tagged `Attack`) via `Attack`(trigger)+`AttackIndex`(i 1–4): AnyState→Attack on trigger (legacy `TriggerAttack` path); the wrench swing instead `Play`s the attack state directly to freeze/scale it (see `FlynnAnimationDriver`). Regenerate clips via **Flynn → Setup Animations**.

**FacingDir octant system retired (2026-06-06).** The driver no longer classifies movement into compass octants or flips the sprite — `FaceWorldDirection` is a no-op and the `FacingDir` param is unused. Direction now comes entirely from the velocity blend tree. (Diagonal placeholder clips `Flynn_{Idle,Run,Jump}_{BackDiag,FrontDiag}` and the New-Character single-frame sprites still exist as blend-tree fodder.)

## Gaps / TODO
- Swing harvests `ResourceNode`s (damage + drops + shake + HP bar + echo-shard chance). Throw still has **no gameplay effect**.
- Tool gating not enforced beyond "wrench is active" (configs declare `requiredTool` but harvest doesn't yet check it).
- World-item art is temp placeholder squares (tinted); resource node temp sprites too — awaiting real art.
- Player is hand-placed at the map spawn — not data-driven.
- Drag-to-drop drops the whole stack (no split); G drops one. No drag-to-reorder between slots yet.

---
**LLM: after changing the player, update this file** (components, current state, gaps). Keep it short and accurate.
