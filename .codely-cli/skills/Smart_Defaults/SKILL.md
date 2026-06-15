---
name: Smart_Defaults
description: Unity Smart_Defaults expert - Smart Generation Defaults Workflow
---
# Smart Generation Defaults Workflow

Expert guidance for determining optimal parameters when user specs are missing.

## 1. Context Gathering
- **Analysis**: Detect Render Pipeline (URP/Built-in) and Build Target.
- **Vision**: Use `GetImageAssetContent` to visually analyze existing project assets.
- **Capture**: Proactively offer to capture a scene screenshot for lighting/style context.

## 2. Decision Logic
- **Pixel Art**: If assets are â‰¤ 64px, generate at 1024px with "pixel art" prompt and use Point filter.
- **Characters**: Always specify "**Full body, head to toe**" and "side view" (2D) or "T-pose" (3D).
- **Scale**: Ensure generated assets match the scale of existing scene objects.

## 3. Workflow Pattern
1. **Concept First**: Generate static concept with `waitForCompletion=true`.
2. **Approval**: Show to user.
3. **Production (Parallel)**: Generate dependent animations concurrently using the concept ID.

## 4. Loop Parameter
- **Always set `loop=false`** regardless of animation type. Looping is an Animator setting.
