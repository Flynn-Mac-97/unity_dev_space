# Ember Valley — Main Menu Concept

**Style:** Warm minimal solarpunk  
**Mood:** Optimistic, handcrafted, calm

## Design Tokens

| Token | Value | Role |
|-------|-------|------|
| `--color-bg` | `#1A2318` | Deep forest backdrop |
| `--color-panel` | `#253220` | Card backgrounds |
| `--color-text` | `#F2ECD8` | Primary cream text |
| `--color-text-muted` | `#B8AD8A` | Secondary text |
| `--color-accent` | `#C9A84C` | Brass gold |
| `--color-accent-hover` | `#DDB85A` | Accent hover |

## Files

- `MainMenu.uxml` — Screen structure
- `MainMenu.uss` — Full styling (tokens, layout, typography, buttons, states)
- `MainMenuController.cs` — Behavior (stub handlers)
- `PanelSettings.asset` — ScaleWithScreenSize @ 1920×1080

## To swap concepts

1. Change the UIDocument's `sourceAsset` to point to another concept's UXML
2. Change `m_PanelSettings` to the concept's PanelSettings asset
3. Swap the controller component

## Ideas to explore next

- Warm earthy variant (terracotta + sage)
- Dark mode variant (charcoal + warm amber)
- Animated title reveal
- HUD overlay concept
- Dialogue screen concept
