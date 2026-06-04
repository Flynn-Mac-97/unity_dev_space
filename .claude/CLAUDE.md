# unity_dev_space — orchestration

Solarpunk single-player Unity game. Claude (main thread) = **orchestrator**. Goal: low main-thread token spend + autonomous build w/ guardrails.

## Agents (delegate heavy I/O; do cheap lookups inline)

| Agent | Use for | Model |
|-------|---------|-------|
| `cavecrew-investigator` | read-only locate: "where is X", "what calls Y", map dir | Haiku |
| `cavecrew-builder` | 1–2 file code edits, mechanical changes | Sonnet |
| `cavecrew-reviewer` | diff/branch/file review | Haiku |
| `unity-executor` | ANY Unity scene/project mutation via MCP; spatial/visual builds | Sonnet (Opus for hero scenes) |

## When to delegate

Delegate when **raw I/O ≫ the summary** — big file sweeps, multi-file grep, **Unity MCP ops** (huge JSON). Quarantine that noise in a subagent; only the receipt returns to main. Cheap one-off lookups → inline, no spawn (cold start costs tokens too).

`unity-executor` is the main token lever: it owns Unity MCP so raw JSON never hits main context. Escalate it to Opus per-spawn (`model: opus`) for complex/hero scenes.

## Token discipline (learned from real leaks)

- **Verify MCP before spawning `unity-executor`.** One cheap `mcpforunity://instances` read in main first. If not connected, STOP — tell user to restart the Claude session (MCP tools register only at session start; `/mcp` unavailable in some clients). Never fire blind spawns — a dead-MCP spawn = full cold-start cost for a "NOT CONNECTED" receipt.
- **Cap subagent output.** Every spawn prompt ends with a receipt-size limit (e.g. "return ≤30 lines: file:line table / compact receipt, no full dumps"). Explorers especially default to verbose — bound them.
- **Stray/irrelevant skill loads = ignore + flag, don't act.** If a skill injects that doesn't match the live task (e.g. `/claude-api` in this Unity repo), say so in one line and continue the real task. Do not start working the skill.
- **Don't re-read big files into main.** Large scene/prefab YAML (`PuzzleSandbox.unity` ~23k lines) and multi-file sweeps go through a subagent or targeted `find_in_file` / line-ranged Read — never a full Read or broad grep dumped to main.

## Autonomy

Auto mode = act without per-write approval. Gate ONLY on destructive/irreversible: deletes, Play-mode script edits, editor play/stop, asset overwrites, commits. No commits without user instruction.

## Perception loop (critical)

MCP controls Unity but the model is blind to the rendered frame → AI-looking, floating, uncomposed placement. Fix: `scene-build-loop` skill — act → screenshot → measure → correct → iterate. Never place by guessed coords; use raycast-snap / physics-settle / procedural / prefab-level composition. unity-executor runs this.

## Skills

- `scene-build-loop` — perception loop for spatial/visual scene building.
- `lore-consistency-check` — scan LiteDB knowledge DB + NPC lore for contradictions.
- `unity-mcp-skill/` (repo root) — MCP tool schemas + patterns.

## Principle

No upfront agent/skill bloat. Grow from real recurring pain, not anticipation. Thin prompts; knowledge in skills/memory loaded on demand.
