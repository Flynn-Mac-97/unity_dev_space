# Flynn UI Draft Palette — Token Reference

> Referenced by `ProjectInstructions.md`. Only relevant during UI work.

## Aesthetic

Flat black surfaces, 1px white outlines, square corners, one type family, generous whitespace, zero ornament.

**Do not** add: rounded corners, drop shadows, gradients, glow, glassmorphism, blur, neon, colored buttons, decorative icons, second accent colors, transparency on surfaces, animated backgrounds.

## Tokens

```css
/* Colors — monochrome only */
.token-bg            { background-color: rgb(0, 0, 0); }
.token-surface       { background-color: rgb(0, 0, 0); }
.token-outline       { border-color: rgb(255, 255, 255); }
.token-text          { color: rgb(255, 255, 255); }
.token-text-muted    { color: rgba(255, 255, 255, 0.6); }
.token-text-faint    { color: rgba(255, 255, 255, 0.35); }
.token-inverse-bg    { background-color: rgb(255, 255, 255); }
.token-inverse-text  { color: rgb(0, 0, 0); }
.token-danger        { color: rgb(255, 59, 48); }
.token-success       { color: rgb(52, 199, 89); }

/* Spacing: xs=4  sm=8  md=16  lg=24  xl=32  2xl=48  3xl=64 */
/* Borders: always 1px, always pure white, always square */
/* Type: one family, six sizes (12/14/16/20/28/40px), no italics, two weights max */
/* Numbers/debug → monospace. Everything else → sans. */
/* Buttons: .btn (default), .btn-primary (exactly one per screen max). All 4 states defined. */
```

## Minimal Screen Anatomy

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="Root" class="token-bg" style="flex-grow: 1; padding: 32px;">
        <ui:Label text="Screen Title" class="text-2xl token-text" />
        <ui:VisualElement class="divider" />
        <ui:VisualElement name="Body" class="panel col" style="margin-top: 24px;">
            <ui:Label text="Section header" class="text-lg token-text" />
            <ui:Label text="Body copy goes here." class="text-base token-text-muted" style="margin-top: 8px;" />
        </ui:VisualElement>
        <ui:VisualElement name="Actions" class="row" style="margin-top: 24px; justify-content: flex-end;">
            <ui:Button text="Cancel" class="btn" style="margin-right: 8px;" />
            <ui:Button text="Confirm" class="btn btn-primary" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

If a screen needs something not expressible in these tokens, **stop and ask** — don't invent a new visual language inline.
