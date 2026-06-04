---
name: scene-build-loop
description: Close the perception loop when building Unity scenes via MCP. Use when placing sprites, 3D props, layouts, or UI through MCP so results look composed, not AI-placed. Methodology - act, capture multi-view screenshots, measure bounds/overlap, critique vs intent, correct via raycast-snap/physics-settle/procedural rules, iterate. Run by unity-executor (vision model).
---

# Scene Build Loop

## Why

MCP controls Unity but the model never sees the rendered frame. Placing by guessed coords → floating, clipping, mis-scaled, uncomposed ("AI-looking"). Fix: close the loop. **Act → see → measure → correct → repeat** until thresholds met. This is how a dev works; emulate it.

## The loop

1. **Act.** Make the change (create/place/scale). Prefer constraint-based placement over raw coords (see below).
2. **See — multi-view screenshot [PRIMARY tool].** Capture the target from **top, front, and perspective**. Read the PNGs back. One view hides depth/overlap; three reveals it.
3. **Measure.** Pull numbers: `Renderer.bounds`, overlap %, gaps, ground contact. Model reasons on numbers where it can't on pixels.
4. **Critique vs intent.** Compare screenshot + numbers to the goal (and any reference image). Name concrete defects: "crate floats 0.4u above ground", "two trees overlap 30%", "sprite z-fighting".
5. **Correct.** Apply fixes (snap, settle, respace). 
6. **Repeat** until thresholds: 0 floating, overlap < ~5% (unless intended), aligned to grid/anchor, on-screen + framed.

## Step 2 — multi-view capture (build this first)

Capture via `execute_code`: spawn a TEMP camera, render to RenderTexture → EncodeToPNG → temp path, then `Read` the PNG. **Validated 2026-06-03** on a real sprite target — works end-to-end.

### Use the compiled `SceneContext` helper (do NOT send code chunks)

Capture + scene context live in a compiled editor helper: `Assets/AgentTools/Editor/SceneContext.cs` (assembly `AgentTools.Editor`, editor-only). Call it with **one-line** `execute_code` — no 30-line dumps, no C#6/CodeDom gymnastics (the helper is real project code; the call is trivial). Validated 2026-06-03 on a real sprite target.

```
return AgentTools.SceneContext.CaptureViews("Wrench_Pickup");      // -> 3 PNG paths (high/front/persp), newline-joined
return AgentTools.SceneContext.Capture("X", 60f, 45f, 1024);        // -> single custom-angle PNG (elev, yaw, size)
return AgentTools.SceneContext.Describe("X");                       // -> id, pos, bounds, components, child count
return AgentTools.SceneContext.ListRenderables(30);                 // -> renderable roots | size | renderer count
return AgentTools.SceneContext.CaptureFromCamera("Main Camera (1)"); // -> PNG from a REAL scene camera (player viewpoint). Use to verify "as the player sees it".
```

Then `Read` each returned PNG path as an image. Not-found returns `ERR: ... — did you mean: <names>`. PNGs land in `%TEMP%/AgentCaptures/` (single-session; OS may GC).

**In-world TMP/text labels:** authoring rotation `[0,0,0]` reads correctly for the scene's Main Camera (at −Z looking +Z — Unity-conventional). A `[0,180,0]` yaw points the readable +Z face AWAY → mirrored text. Verify label-facing with `CaptureFromCamera`, not synthetic angles. The project `Billboard` component (`Assets/Flynn/Scripts/Common/Billboard.cs`) is runtime-only (Start/LateUpdate, pitch-only) so it does NOT orient static labels in edit mode — don't rely on it for authored text.

`CaptureViews` already uses the 2D/2.5D-correct angles: **`high` (60° elevation)** — a billboard sprite from straight-down (90°) is near-invisible (zero depth). `front` eye-level, `persp` 45°/45°. For true 3D meshes a straight-down top is also fine — use `Capture(name, 90, 0)`.

**Extend the helper, not the call.** New context needs (overlap %, ground-snap, gaps) → add a static method to `SceneContext.cs`, recompile once, then invoke by name. Keep `execute_code` bodies one line. After editing the helper: refresh → wait `isCompiling` → `read_console` before use. Never edit it while `isPlaying`.

Compiler: **Roslyn C# 12 is installed** → `execute_code` (`auto`) uses modern syntax (usings, tuples, local funcs). The old C#6/CodeDom limits only apply if Roslyn is ever missing — then fall back to fully-qualified types, no top-level `using`, no local funcs/tuples. Either way prefer fixing/recompiling the `SceneContext` helper over inlining; inline only one-offs.

## Token economy (do this — it's the difference between cheap and expensive)

1. **Build via data, not procedure.** To construct/lay out a level, ship a COMPACT SPEC to `AgentTools.SceneBuilder.Build(spec)` — do NOT hand-write a long imperative `execute_code` script. The "how" is compiled; you send ~30 lines of spec, not ~200 of C#. `Build` is **idempotent** (`root NAME` wipes + rebuilds), so iterating costs one tiny spec. Spec verbs: `root`, `ground`, `tile`, `label`, `prim <shape>`, `tree`, `crate`, `rock`, `soil` (`;`-separate points for repeats). Tints by name or `#hex`. Labels auto-authored at `[0,0,0]`. See `SceneBuilder.cs` header for the full grammar.
2. **Measure in numbers before you look.** `Describe` / `Overlaps` / `ListRenderables` are ~free text and answer "is it placed right?" without an image. Use them first.
3. **Screenshots are the premium token — spend last, small, once.** Verify with `CaptureMontage(target, 512)` = 3 views in ONE image (one image << three). Capture at the END, not per-iteration; only re-shoot after a correction. Use `CaptureFromCamera` only when label-facing / player-view matters (it's authoritative for text orientation; the montage top view can mirror labels).
4. **Probes are text-only.** Gathering facts (axes, coords, ground, components) needs no screenshot — skip the image.

## Constraint-based placement (avoid blind coords) — compiled API

All in `AgentTools.SceneContext`, one-line `execute_code`. These MUTATE the scene (the build itself); moves use `Undo.RecordObject` so they're reversible. Validated 2026-06-03.

```
return AgentTools.SceneContext.GroundSnap("Crate");           // drop onto nearest collider below; renderer bottom rests on surface. ERR if no ground collider.
return AgentTools.SceneContext.GroundSnap("Crate", 80f);      // maxDrop override
return AgentTools.SceneContext.Settle("Crate,Barrel", 120);   // edit-mode physics: temp Rigidbody -> Physics.Simulate N steps -> strip RB. Props need Colliders. Returns per-obj old->new.
return AgentTools.SceneContext.Overlaps(null);                // all renderable roots: per-pair per-axis overlap extents. null=all, or "A,B,C".
```

Workflow: place roughly → `GroundSnap` (kills floating) and/or `Settle` (props rest/stack naturally) → `Overlaps` to verify no interpenetration → `CaptureViews` to eyeball. Iterate.

Caveats: `GroundSnap`/`Settle` need a **Collider on the ground** (and on settling props) — kinematic-Rigidbody floors work too. `Overlaps` uses world-AABB (`Renderer.bounds`) → rotated objects report inflated boxes (possible false positives); fine for a quick scan.

Still do in `execute_code` directly (not yet in the helper): **anchor/socket** parenting (local offset, not world coord) and **procedural** layout (grid+jitter / Poisson for foliage/props/tiles → coherent distribution). Add to the helper if they recur.

## Notes

- No script edits while `isPlaying` (LiteDB seed). Check `editor_state` first.
- Returns to unity-executor as a compact receipt — not raw image bytes or JSON.
