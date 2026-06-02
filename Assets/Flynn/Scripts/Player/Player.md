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
| `SolarpunkCharacterController` | 4-dir velocity move + impulse jump on 3D Rigidbody; raycast ground check. Exposes `MoveInput`/`IsGrounded`/`NormalizedSpeed`. |
| `FlynnAnimationDriver` | Controller → Animator (`Speed`/`IsGrounded`/`FacingDir`); flips sprite, billboards Visual. API: `TriggerAttack(idx)`, `FaceWorldDirection(dir)`. |
| `PlayerMouseAimer` | Mouse→world `WorldAimPoint`; nearest `HoveredResource` / `HoveredPickup`. Read-only. |
| `PlayerInventory` | 4 slots; copies `InventoryData` SO on Awake. Hotkeys 1–4 select active slot. `ActiveItemType`, `HasItem`, `TryAddItem` (wrench→slot 0, resources→1–3). Events for Hotbar. |
| `PlayerPickupController` | **E** + aim at a `WorldItemPickup` → `TryAddItem`, destroy world object. |
| `WrenchSwingController` | **LMB**: tap = light swing, hold = charge → heavy swing (longer cooldown). Faces mouse. Uses hovered resource's tool anim, else wrench (idx 4). |
| `WrenchThrowController` | **RMB**: tap = short throw, hold = charge → farther. Spawns `ThrownWrench`. One airborne at a time. |
| `PlayerAimReticle` | Ground arrow at the player pointing along aim; grows/recolors with charge. |

**Wrench abilities require the wrench to be the _active_ slot** (`ActiveItemType == Wrench`), not merely owned. Disabled while a throw is airborne.

## Wrench subsystem (the multitool)
- `WrenchConfig` (SO, `Configs/Player/WrenchConfig.asset`) — all swing/throw tunables (charge times, heavy threshold, cooldowns, throw distances/speeds, boomerang return delay, catch radius).
- `ThrownWrench` — projectile: outbound → hover (`returnDelay`) → boomerang home → caught (`OnCaught`). Billboards + spins in screen plane. Square placeholder sprite.

## HUD (UI Toolkit, Flynn wireframe palette)
`UI/Screens/WrenchHud/` (UXML+USS+`WrenchHudController`): mouse **aim reticle** (follows cursor) + **charge meter** (fills while charging, light/heavy tick). Controller reads the swing/throw controllers via Inspector refs (no scene lookups). Needs its own UIDocument GO in the scene.

## Data & world targets (same folder)
- `ItemType` (None/Pick/Axe/Hammer/Wrench), `ItemDefinition` SO, `InventoryData` SO (slot 0 empty so the wrench is picked up).
- `WorldItemPickup` — trigger granting an `ItemDefinition` (static `NearbyPickups`).
- `ResourceNode` — harvestable trigger; maps tool→attack index (static `NearbyNodes`).
- Placeholder square sprite: `Sprites/placeholder_square.png` (used by wrench pickup, thrown wrench).

## Animator (`Flynn_AnimatorController`)
States: idle/run/jump × front/back/side + `Attack_{Pick,Axe,Hammer,Wrench}`.
Params: `Speed`(f), `IsGrounded`(b), `FacingDir`(i 0=front/1=back/2=side), `Attack`(trigger), `AttackIndex`(i 1–4). AnyState→Attack on trigger+index.

## Gaps / TODO
- Swing & throw have **no gameplay effect** yet (no resource depletion / drops / damage). Light-vs-heavy only differs in cooldown.
- Hit/impact feedback not built (only charge buildup is shown).
- Tool gating not enforced beyond "wrench is active."
- Player is hand-placed at the map spawn (~8.5, 0.5, 6.5) — not data-driven.

---
**LLM: after changing the player, update this file** (components, current state, gaps). Keep it short and accurate.
