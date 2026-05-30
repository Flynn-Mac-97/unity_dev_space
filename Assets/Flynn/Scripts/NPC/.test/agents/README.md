# NPC Battery Agents

This folder separates the NPC test workflow into two lightweight agent roles.

- `npc-battery-converser.agent.md` runs the existing Unity/C# battery harness through MCP and stores raw conversation data.
- `npc-battery-analyzer.agent.md` reads stored cycle data and writes compact analysis artifacts.

The split keeps expensive transcripts out of chat context. The converser produces data; the analyzer consumes it.
