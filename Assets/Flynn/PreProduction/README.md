# Flynn PreProduction — island authoring pipeline

Two-stage workflow for authoring island/community content for the LLM-NPC system.
Design the **story** first, then turn it into schema-bound game data. Each stage is a
**conversational discipline** you work with and refine *before committing* — not a
one-shot generator.

```
rough idea
  → Narrative Designer  ──► Story Bible (Bibles/<id>.md)     "the soul"
  → Island Content Creator ──► IslandContent.json            "usable data"
  → IslandContentValidator
  → human review
  → game runtime (IslandContentHub)
```

## The two agents

| Stage | File | Role | Mindset |
|-------|------|------|---------|
| 1 | `NarrativeDesigner.agent.md` | Idea → Story Bible. Creative, thematic, schema-free. | "What is the soul of this place?" |
| 2 | `IslandContentCreator.agent.md` | Story Bible → `IslandContent.json`. Practical, schema-bound, invents nothing unsupported. | "How does this story become usable game data?" |

**Story agent writes meaning. JSON agent builds data.**

## Where things live

- **Story Bibles** (working files): `Assets/Flynn/PreProduction/Bibles/<island_id>.md`
- **Runtime JSON** (the deliverable): `Assets/Flynn/Configs/NPC/Islands/<island_id>.json`
- **Schema / contract** (authoritative): `Assets/Flynn/Configs/NPC/Islands/island.schema.json`,
  `Assets/Flynn/Scripts/NPC/Runtime/Content/IslandContent.cs`,
  `…/IslandContentValidator.cs`
- **Gold-standard example**: `Assets/Flynn/Configs/NPC/Islands/windroot_hamlet.json`

## How to invoke

These are reference disciplines, not auto-spawned subagents. Point Claude at them:

- *"Act as the Narrative Designer in Flynn/PreProduction — let's design an island where the tide-mill stopped."*
- *"Act as the Island Content Creator in Flynn/PreProduction — build JSON from Bibles/tide_mill.md."*

Keep `<island_id>` identical across the bible and the JSON (lower_snake_case) so the two
stages line up.

> Replaces the old one-shot `island-designer` skill, which jumped straight from brief to
> JSON. Its knowledge (content limits, heuristics, allowed handlers, validation recipe,
> scene-id preservation) now lives inside the two agents above.
