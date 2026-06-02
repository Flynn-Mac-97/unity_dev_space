# Map Loader

**Code:** `Assets/Flynn/Scripts/MapGeneration/MapLoader.cs` — component on `MAP_LOAD_MANAGER`.
Scene: `Assets/Flynn/Map_Loader.unity`.

## Purpose
Loads `map.json` (from the external map editor) and builds the world: SpriteShape ground, per-tile layer items (decals/resources/npcs/sprites), and a single box ground collider (top at **Y=0**).

## Flow (menu *Load And Generate Map*, or on play)
1. Parse JSON → `MapData`.
2. Build ground SpriteShape(s); flat-rotate XY→XZ.
3. Spawn layer items under child roots: `GROUND` / `DECALS` / `NPC` / `SPRITES` / `RESOURCES`.
4. `GenerateGroundCollider` — one BoxCollider sized to the map.
5. `SpawnPlayer` (see below).

## Conventions
- Spawned objects named `<Prefix>_<id>_<key>` (`Ground_`/`Decal_`/`Resource_`/`Npc_`/`Sprite_`/`Player_`); cleared by prefix.
- Tile (x,y) → world (x, *, y); map-Y becomes world-Z.
- NPC `typeId 3000` = player-start tile.

## Player spawning (IMPORTANT)
`spawnPlayerAtRuntime` (Inspector) gates `SpawnPlayer`.
- **In `Map_Loader.unity` it is OFF** — the player is a persistent scene instance of `Player.prefab`, hand-placed on the ground. Keep OFF whenever a scene already contains a placed player (prevents duplicate players).
- When ON: `Instantiate`s `playerPrefab` at the 3000 tile (`playerSpawnYOffset` above Y=0) and points the Cinemachine vcam at it.

## SpriteShape quirks
- Clones share splines — replace via reflection (`InstantiateGroundShape`).
- Rebuild order: `RefreshSpriteShape` → `UpdateSpriteShapeParameters` → `BakeMesh().Complete` → `RefreshSpriteShape`.
- A hidden template SpriteShape stays in the scene (renderer disabled) — don't delete it.

## Current state
Working JSON pipeline. Procedural sub-generators it can dispatch to live in the same folder (`IslandGeneratorTwo` preferred over `IsleGenerator`).

## Gaps / TODO
- With runtime spawn OFF, player placement is manual (not driven by the 3000 tile).

---
**LLM: after changing the map loader, update this file.** Keep it short and accurate.
