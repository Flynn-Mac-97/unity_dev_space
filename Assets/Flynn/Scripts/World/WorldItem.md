# World Item / Drop System

**Code:** `Assets/Flynn/Scripts/World/`
**Prefabs:** `Assets/Flynn/Prefabs/Items/{Wood,Stone,MetalScrap,EchoShard}_Drop.prefab` (temp tinted placeholder sprites).

The one system for anything that can sit on the ground and be collected — resource drops, echo shards, items dropped from the inventory, and (eventually) the world wrench. Built so new item kinds need only an `ItemDefinition` + a drop prefab, no new code.

## Components
| Script | Role |
|---|---|
| `WorldItem` | Data carrier: `ItemDefinition item` + `int count`. Solid (non-trigger) collider, raycast-detected (no registry). `Configure(item,count)`, `Reduce(n)`. |
| `WorldItemSpawner` (static) | Single spawn entry point. `Spawn(item, count, pos, popImpulse)` instantiates `item.worldPrefab`, configures the `WorldItem`, applies an upward pop (needs a Rigidbody). Used by resource drops, echo-shard rolls, and player drops. |
| `DroppedItemMagnet` | On auto-collect items. After a settle delay, if there's room (`PlayerInventory.HasRoomFor`, currency always has room) it eases to the player and collects: items → `TryAddItem`, currency → `ItemDefinition.currencyTarget.Add`. If the inventory fills mid-flight it drops the remainder and idles. Finds the player via a `PlayerAnchor` SO (no `FindObjectOfType`). `Suppress(seconds)` delays re-collection of player-dropped items. |

(Flat 2D: drops are plain `SpriteRenderer`s — no billboard.)

## Collection routing (set on `ItemDefinition`)
- `autoCollect == true` → magnets to the player (resources). `false` → key-pickup (the wrench): the player aims and presses **E** (`Player/Interaction/WorldItemPickup`).
- `isCurrency == true` → never enters a slot; collecting adds to `currencyTarget` (`IntVariable`, e.g. `EchoShardCount`). `AutoCollects` is true for currency too, so shards fly in.
- `worldPrefab` → the prefab spawned to represent the item on the ground.

## Prefab recipe (drop items)
Root scale ~0.4, layer **Pickup (9)**: SpriteRenderer (temp tint) + CircleCollider2D (solid, r≈0.25) + Rigidbody2D (freeze rotation; gravityScale per pop behaviour) + `WorldItem` + `DroppedItemMagnet` (anchor = `PlayerAnchor.asset`).

## Wiring (SO assets, no singletons)
`PlayerAnchor` (player Transform, set by `PlayerAnchorRegistrar`), `EchoShardCount` (`IntVariable`), `ResourceHitChannel` (resource→HUD). All Inspector-assigned.

## Gaps
- Temp art only (placeholder squares).
- No stack-split on drag-drop (drops whole stack); no inter-slot reorder.
- World wrench in the scene still uses the deprecated `WorldItemPickup`; not yet migrated to a key-pickup `WorldItem`.

---
**LLM: after changing the world-item/drop system, update this file.**
