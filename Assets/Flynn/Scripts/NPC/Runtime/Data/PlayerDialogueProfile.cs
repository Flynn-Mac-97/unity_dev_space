using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Player Dialogue Profile", fileName = "PlayerDialogueProfile")]
public class PlayerDialogueProfile : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Traveler";

    [TextArea(2, 4)]
    [Tooltip("One short paragraph describing who the player is from the world's point of view. The LLM reads this so it can suggest dialogue options that sound like the player.")]
    public string persona = "A young traveler new to these islands. Plain-spoken, curious, polite.";

    [TextArea(1, 3)]
    [Tooltip("How the player tends to talk. Short, declarative phrases work best (e.g. 'Asks rather than tells. Short sentences. Direct.').")]
    public string speakingStyle = "Asks rather than tells. Short sentences. Curious tone.";

    [TextArea(1, 3)]
    [Tooltip("Optional: things the player is interested in, knows, or is currently doing. Helps options feel grounded.")]
    public string currentSituation = "Exploring the islands. Looking for things worth knowing.";

    public string GetSafeDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? "Traveler" : displayName.Trim();
    }
}
