using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/NPC Personality Profile")]
public class NpcPersonalityProfile : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique key used for save-based memory lookup.")]
    public string npcId = "npc.stranger";

    public string displayName = "Stranger";

    [Header("Visual Assets")]
    [Tooltip("Optional in-world sprite used for this NPC.")]
    public Sprite inGameSprite;

    [Tooltip("Optional portrait sprite used in dialogue and menus.")]
    public Sprite portraitSprite;

    [Header("Persona Details")]
    [Tooltip("Source data for the prompt template. Reference these from the per-NPC prompt template with tokens like {role}, {speaking_style}, {personality_traits}, {do}, {dont}.")]
    [TextArea(2, 4)]
    public string roleDescription = "A friendly local who knows the area.";

    [TextArea(2, 4)]
    public string speakingStyle = "Short, practical, warm.";

    [TextArea(2, 6)]
    public string personalityTraits = "Helpful, cautious, curious.";

    [TextArea(2, 6)]
    public string doRules = "Answer in plain language. Stay in character.";

    [TextArea(2, 6)]
    public string dontRules = "Do not mention being an AI model. Do not break setting tone.";

    [Header("Capabilities")]
    [Tooltip("Concrete in-game actions this NPC can actually perform. Surfaced to the LLM via the {capabilities} token so the NPC doesn't offer things the game cannot do.")]
    public List<string> capabilities = new List<string>();

    [Tooltip("Short lines this NPC is keen to bring up unprompted. Used in the legacy assembly path; for system-prompt NPCs, fold these into the prose instead.")]
    public string[] conversationHooks = new string[0];

    [Header("Fallback")]
    [TextArea(2, 5)]
    public string[] fallbackLines =
    {
        "I need a moment to think.",
        "I am not sure, but we can keep talking.",
        "Tell me that one more time."
    };

    public string GetSafeDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? "Unknown NPC" : displayName.Trim();
    }

    public string GetSafeNpcId()
    {
        return string.IsNullOrWhiteSpace(npcId) ? "npc.unknown" : npcId.Trim();
    }
}
