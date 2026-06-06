# unity_dev_space — orchestration

Solarpunk single-player Unity game. Claude (main thread) = **orchestrator**. Goal: low main-thread token spend + autonomous build w/ guardrails.

## Agents (delegate heavy I/O; do cheap lookups inline)

| Agent | Use for | Model |
|-------|---------|-------|
| `cavecrew-investigator` | read-only locate: "where is X", "what calls Y", map dir | Haiku |
| `cavecrew-builder` | 1–2 file code edits, mechanical changes | Sonnet |
| `cavecrew-reviewer` | diff/branch/file review | Haiku |
| `unity-executor` | ANY Unity scene/project mutation via Unity-Skills REST; spatial/visual builds | Sonnet (Opus for hero scenes) |

## When to delegate

Delegate when **raw I/O ≫ the summary** — big file sweeps, multi-file grep, **Unity-Skills REST ops** (huge JSON + 578 KB schema). Quarantine that noise in a subagent; only the receipt returns to main. Cheap one-off lookups → inline, no spawn (cold start costs tokens too).

`unity-executor` is the main token lever: it is the SOLE caller of the Unity-Skills python client, so raw REST JSON + the skill schema never hit main context. Never call `unity_skills.py` from the main thread. Escalate it to Opus per-spawn (`model: opus`) for complex/hero scenes.

## Unity driver (swapped CoplayDev MCP → Unity-Skills REST, 2026-06-06)

Unity controlled by the **Unity-Skills** plugin (Besty0728, `com.besty.unity-skills`): HTTP server in the Editor + python client `unity_skills.py` + a skill (`SKILL.md` + 69 module docs) installed under `.claude/skills/`. NOT MCP — there are no `mcp__UnityMCP__*` tools anymore. Token reduction is now two-layer: (1) plugin cuts ~96% at source (schema cached once, batch-first, compact JSON), (2) `unity-executor` still quarantines whatever's left. Both must hold.

## Token discipline (learned from real leaks)

- **Verify the server before spawning `unity-executor`.** Server runs only after `Window > UnitySkills > Start Server` in the Editor. If the agent reports `SERVER DOWN`, tell the user to start it — don't re-spawn blind.
- **Never pull the 578 KB skill schema to main.** Discovery (`find_skills` / category filter) happens inside the agent, once. Module docs load on demand by topic, inside the agent.
- **Cap subagent output.** Every spawn prompt ends with a receipt-size limit (e.g. "return ≤30 lines: file:line table / compact receipt, no full dumps"). Explorers especially default to verbose — bound them.
- **Stray/irrelevant skill loads = ignore + flag, don't act.** If a skill injects that doesn't match the live task (e.g. `/claude-api` in this Unity repo), say so in one line and continue the real task. Do not start working the skill.
- **Don't re-read big files into main.** Large scene/prefab YAML (`PuzzleSandbox.unity` ~23k lines) and multi-file sweeps go through a subagent or targeted `find_in_file` / line-ranged Read — never a full Read or broad grep dumped to main.

## Autonomy

Auto mode = act without per-write approval. Gate ONLY on destructive/irreversible: deletes, Play-mode script edits, editor play/stop, asset overwrites, commits. No commits without user instruction.

## Perception loop (critical)

REST controls Unity but the model is blind to the rendered frame → AI-looking, floating, uncomposed placement. Fix: `scene-build-loop` skill — act → screenshot → measure → correct → iterate. Never place by guessed coords; use raycast-snap / physics-settle / procedural / prefab-level composition. unity-executor runs this.

## Skills

- `scene-build-loop` — perception loop for spatial/visual scene building.
- `lore-consistency-check` — scan LiteDB knowledge DB + NPC lore for contradictions.
- `unity-skills` (installed under `.claude/skills/` by the Skill Installer) — REST skill schemas + 69 module docs + python client. Replaces the old `unity-mcp-skill/`.

## Principle

No upfront agent/skill bloat. Grow from real recurring pain, not anticipation. Thin prompts; knowledge in skills/memory loaded on demand.
