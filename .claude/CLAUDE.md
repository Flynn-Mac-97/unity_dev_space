# unity_dev_space — orchestration

Solarpunk single-player Unity game. Claude (main thread) = **orchestrator**. Goal: low main-thread token spend + autonomous build w/ guardrails.

## Agents (delegate heavy I/O; do cheap lookups inline)

| Agent | Use for | Model |
|-------|---------|-------|
| `cavecrew-investigator` | read-only locate: "where is X", "what calls Y", map dir | Haiku |
| `cavecrew-builder` | 1–2 file code edits, mechanical changes | Sonnet |
| `cavecrew-reviewer` | diff/branch/file review | Haiku |
| `unity-executor` | ANY Unity scene/project mutation via the **direct TCP bridge** (`unity_bridge.py`); spatial/visual builds. Live editor, autonomous, zero LLM at the wire. | Sonnet (Opus for hero scenes) |

The three `cavecrew-*` agents come from the **caveman plugin** (`JuliusBrussee/caveman`, user-scope). investigator/reviewer pin `model: haiku` in the plugin; **builder does NOT pin a model** — spawn it with `model: sonnet` to honor the table (else it inherits Opus). `unity-executor` is project-local (`.claude/agents/`).

## When to delegate

Delegate when **raw I/O ≫ the summary** — big file sweeps, multi-file grep. Quarantine that noise in a subagent; only the receipt returns to main. Cheap one-off lookups → inline, no spawn (cold start costs tokens too).

**Unity live ops → `unity-executor`** (see Unity driver). It is the sole caller of `unity_bridge.py`; raw bridge JSON stays in the subagent, only the receipt returns to main.

## Unity driver (direct TCP bridge — 2026-06-23. Was: Codely test → Unity-Skills REST before that)

Unity driven by a **direct TCP bridge to the live editor**: `unity_bridge.py` (repo root) speaks the Tuanjie Codely bridge protocol (`cn.tuanjie.codely.bridge`, port from `.com-unity-codely.json`). **Reversed from the Codely binary 2026-06-23** — we don't run Codely at all; we talk to the editor's TCP server straight. Best of all options: live editor + autonomous from main + **zero LLM cost at the wire** (plain sockets).

- **`unity-executor` is the sole caller.** It runs a Python script that imports `UnityBridge`, does the work, prints a compact receipt. Raw JSON quarantined in the subagent. Never import `unity_bridge` from the main thread (JSON is big).
- Commands + action vocab: `unity_bridge_commands.md`. 13 `manage_*`/`execute_*` commands incl. `execute_csharp` (arbitrary editor scripting escape hatch).
- **Precondition: the Unity editor must be running** (the editor *is* the server). If the agent reports `BRIDGE DOWN`, tell the user to open the editor — don't re-spawn blind.
- **Proxy gotcha:** prefix every call `NO_PROXY="localhost,127.0.0.1" HTTP_PROXY="" HTTPS_PROXY=""` (settings.local may set a proxy that breaks the localhost socket).
- Protocol (for reference): server greets `WELCOME UNITY-TCP 1 FRAMING=1 SERVER_VERSION=2\n`; then 8-byte big-endian length-prefixed frames; request `{"type":<cmd>,"params":{"action":<action>,...},"request_id":<id>}`; payload nested at `resp["data"]["data"]`.

**Fallbacks (dormant, on disk):**
- *Codely* (`codely` CLI / Tuanjie Cowork app) — only needed if the bridge protocol breaks on a Codely update. Two surfaces: `codely -p` is headless but **lacks live Unity tools** (file/code only); the **in-app assistant** has the tools but is human-driven. We bypass both by talking to the TCP server directly.
- *Unity-Skills REST* (`com.besty.unity-skills` + `.claude/skills/unity-skills/` + `unity_skills.py`) — the prior driver. **Never call `unity_skills.py` from the main thread.**

## Token discipline (learned from real leaks)

- **Unity work → `unity-executor`, never import `unity_bridge` inline.** Don't pull bridge JSON into main. The agent runs the Python, returns a receipt.
- **Cap subagent output.** Every spawn prompt ends with a receipt-size limit (e.g. "return ≤30 lines: file:line table / compact receipt, no full dumps"). Explorers default to verbose — bound them.
- **Stray/irrelevant skill loads = ignore + flag, don't act.** If a skill injects that doesn't match the live task (e.g. `/claude-api` in this Unity repo), say so in one line and continue the real task. Do not start working the skill.
- **Don't re-read big files into main.** Large scene/prefab YAML (`PuzzleSandbox.unity` ~23k lines) and multi-file sweeps go through a subagent or targeted `find_in_file` / line-ranged Read — never a full Read or broad grep dumped to main.

## Autonomy

Auto mode = act without per-write approval. Gate ONLY on destructive/irreversible: deletes, Play-mode script edits, editor play/stop, asset overwrites, commits. No commits without user instruction.

## Perception loop (critical)

The bridge controls Unity but the model is blind to the rendered frame → AI-looking, floating, uncomposed placement. Fix: `scene-build-loop` skill — act → screenshot (`manage_screenshot`) → measure → correct → iterate. Never place by guessed coords; use raycast-snap / physics-settle / procedural / prefab-level composition. `unity-executor` runs this (vision-capable subagent).

## Skills

- `scene-build-loop` — perception loop for spatial/visual scene building. Run by `unity-executor` (now drives `manage_screenshot` via the bridge).
- `lore-consistency-check` — scan LiteDB knowledge DB + NPC lore for contradictions.
- `unity-skills` (installed under `.claude/skills/`) — REST skill schemas + module docs + python client. **Dormant** — superseded by the direct TCP bridge; kept as fallback.
- `caveman` / `caveman-compress` / `caveman-stats` / `cavecrew` — from the caveman plugin (user-scope). `/caveman` toggles compressed output. This repo pins `defaultMode: ultra` via `.caveman.json` (SessionStart hook auto-activates it here only; other repos unaffected). Uninstall: `npx -y github:JuliusBrussee/caveman -- --uninstall`.

## Principle

No upfront agent/skill bloat. Grow from real recurring pain, not anticipation. Thin prompts; knowledge in skills/memory loaded on demand.
