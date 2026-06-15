---
name: Refinement
description: Unity Refinement expert - Refine Generated Asset Expert Instructions
---
# Refine Generated Asset Expert Instructions

Iteration strategy to make modifications feel like editing.

## 1. Understanding Requests
- **Recolor**: Hue shifts or palette changes.
- **In-paint**: Adding or removing specific elements via masking.
- **Upscale**: High-resolution enhancement.
- **Transform**: Flipping or rotating.

## 2. Refine vs Regenerate
- **Refine**: For small changes or quality boosts.
- **Regenerate**: For major style shifts or subject changes. Always use `referenceImageInstanceId` to keep the design spirit.

## 3. Safety
- **ALWAYS create a backup** before destructive refinements.
- Inform the user where the backup is stored.

## 4. Context Retention
- Track original prompts and IDs across turns.
- Apply refinements to the *current* active asset context.
