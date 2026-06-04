---
name: lore-consistency-check
description: Scan the project's LiteDB knowledge DB and island NPC lore for contradictions - the same fact, name, date, or relationship asserted with conflicting values across NPCs or islands. Use after authoring/editing lore or seeding knowledge, or when a player-facing inconsistency is suspected. Grep-first - cheap, targeted, no full-DB dumps.
---

# Lore Consistency Check

## Goal

Find contradictions across the knowledge DB + NPC lore: same entity (person, place, event, date, relationship, item) asserted with **conflicting values** in different NPCs/islands/knowledge entries.

## Grep-first (the one good idea, token-cheap)

Do NOT dump the whole DB. Instead:

1. **Build the entity index once.** Query the LiteDB knowledge collection for entity names/keys only (not full text). One pass → list of known entities + where each is referenced.
2. **Target only references.** For each entity, read only the entries/sections that mention it. Skip everything else. (Their version cut ~4,000 lines scanned → ~500 this way.)
3. **Compare asserted values** per entity across its references.

Discover the DB path + schema first (it's a LiteDB file under the Unity project; check the island-authoring-pipeline + seeding scripts for collection names). Query via the seeding/data layer or a read-only LiteDB read — never mutate.

## What counts as a conflict

- Same fact, different value (NPC A: "the bridge fell in spring", NPC B: "...in winter").
- Same name, different identity/role.
- Broken reference (NPC cites an entity that no longer exists).
- Stale entry (knowledge contradicts the current canonical source).

## Output (categorized)

```
🔴 conflict: <entity> — A:"<val>" (npc/island) vs B:"<val>" (npc/island)
⚠️ stale:    <entity> — entry diverges from canonical <source>
ℹ️ unverifiable: <entity> — referenced, no canonical value to check
```

End with totals: `N conflicts, M stale, K unverifiable.` If clean → `No contradictions.`

## Scope

- Read-only. Report only. Do NOT auto-fix — fixing lore fan-out is `propagate-change` (not built yet).
- Knowledge DB + NPC lore only. Skip code, scene geometry.

## Ties into

`island-authoring-pipeline` (Narrative→Content flow). Run after a content pass on an island.
