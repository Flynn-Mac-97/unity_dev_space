using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/NPC Personality Profile")]
public class NpcPersonalityProfile : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique key used for save-based memory lookup.")]
    public string npcId = "npc.stranger";

    public string displayName = "Stranger";

    [TextArea(2, 4)]
    public string roleDescription = "A friendly local who knows the area.";

    [Header("Visual Assets")]
    [Tooltip("Optional in-world sprite used for this NPC.")]
    public Sprite inGameSprite;

    [Tooltip("Optional portrait sprite used in dialogue and menus.")]
    public Sprite portraitSprite;

    [Header("Voice and Tone")]
    [TextArea(2, 4)]
    public string speakingStyle = "Short, practical, warm.";

    [TextArea(2, 6)]
    public string personalityTraits = "Helpful, cautious, curious.";

    [Header("Guidelines")]
    [TextArea(2, 6)]
    public string doRules = "Answer in plain language. Stay in character.";

    [TextArea(2, 6)]
    public string dontRules = "Do not mention being an AI model. Do not break setting tone.";

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
