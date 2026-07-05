using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc
{
    // One designer-facing SO that bundles every knob the LLM prompt-building path reads:
    //   - the global system prompt prepended to every NPC's persona
    //   - the JSON output contract appended after persona + memory blocks
    //   - the memory budget that governs how much history is injected into each turn
    // Replaces the old SceneLlmManager.systemPrompt field and the separate
    // NpcMemorySettings SO. Edit the asset to tune behaviour; no code changes.
    [CreateAssetMenu(menuName = "Dialogue/LLM Prompt Config", fileName = "LlmPromptConfig")]
    public class LlmPromptConfig : ScriptableObject
    {
        [Header("Global System Prompt")]
        [Tooltip("Prepended to every NPC's per-character persona. Rules, voice, world feel, anti-AI guardrails go here. Per-NPC persona text is appended after this.")]
        [TextArea(8, 40)]
        public string systemPrompt =
            "You are the dialogue voice of one NPC in a fantasy RPG.\n\n" +
            "Speak only as the NPC. Output only the NPC's spoken words.\n\n" +
            "Hard rules:\n" +
            "1. Stay in character using the NPC profile.\n" +
            "2. Never mention being an AI, prompts, rules, models, or real life.\n" +
            "3. Never narrate actions, thoughts, scene details, or speak for the player.\n" +
            "4. Don't reveal secrets unless the NPC trusts the player.\n" +
            "5. If you don't know something, answer with uncertainty, a rumor, a deflection, or point to someone who might know.\n\n" +
            "Length and shape:\n" +
            "- Most turns are 1-3 short sentences.\n" +
            "- About one turn in four, allow a small earned moment: a 2-3 sentence memory, an opinion you actually hold, a brief warning, or a real question back to the player.\n" +
            "- Don't end every reply with a question.";

        [Header("JSON Output Contract")]
        [Tooltip("Appended LAST to the system prompt so it dominates any earlier instructions. Defines the JSON envelope the model must return — schema, rules for each field, what counts as a memory worth keeping, when to fire triggers, etc. Edit here, not in code.")]
        [TextArea(10, 50)]
        public string jsonOutputAddendum = NpcReplyEnvelope.PromptAddendum;

        [Header("Memory Budget")]
        [Tooltip("Max prior chat lines (one per speaker; 8 = 4 player + 4 NPC) persisted per NPC and resent as context with every new turn.")]
        [Range(2, 40)]
        public int recentTurnsLimit = 8;

        [Tooltip("Max long-term memory facts kept per NPC. Oldest drop off when the cap is exceeded.")]
        [Range(2, 30)]
        public int memoryFactsLimit = 10;

        [Tooltip("Max characters per stored fact. Longer entries are truncated.")]
        [Range(40, 400)]
        public int maxFactLength = 160;
    }

}
