# Village Restoration Hub — Concept

**Style:** Warm solarpunk artisan workshop  
**Mood:** Cozy, handcrafted, optimistic  
**Genre:** 2D story game, village restoration

## Design Tokens

| Token | Value | Role |
|-------|-------|------|
| `--color-bg` | `#2B2820` | Warm dark earth |
| `--color-panel` | `#3D382E` | Parchment-dark panel |
| `--color-panel-light` | `#4A4438` | Hover/raised surface |
| `--color-text` | `#F0E8D5` | Cream primary |
| `--color-text-muted` | `#B8AD98` | Secondary |
| `--color-accent` | `#C9A84C` | Brass gold |
| `--color-accent-green` | `#7BA87E` | Sage green |
| `--color-danger` | `#C46B5D` | Rust red |

## Files

- `VillageHub.uxml` — Structure: header, resource bar, 3 task cards, detail panel, bottom bar
- `VillageHub.uss` — Full styling: tokens, layout, cards, NPC badges, requirements, buttons
- `VillageHubController.cs` — Task selection logic, detail population, affordability check, resource display
- `PanelSettings.asset` — ScaleWithScreenSize @ 1920×1080

## Interaction States

- **Task card hover** — raised background, stronger brass border
- **Task card selected** — brass border, opens detail panel below
- **Task card disabled** — 50% opacity, no hover, no click
- **Confirm button** — disabled when unaffordable task selected
- **Requirement met/unmet** — green/red color coding

## To Use

1. Attach UIDocument source to `VillageHub.uxml`
2. Set PanelSettings to `PanelSettings.asset`
3. Add `VillageHubController` component
