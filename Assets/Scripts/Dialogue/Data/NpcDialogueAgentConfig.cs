using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/NPC Dialogue Agent Config")]
public class NpcDialogueAgentConfig : ScriptableObject
{
    [Header("Required Assets")]
    public NpcPersonalityProfile personalityProfile;
    public NpcPromptTemplate promptTemplate;
    public NpcMemorySettings memorySettings;

    [Header("Behavior")]
    [Tooltip("When true, use local model when available. Otherwise rely on fallback text.")]
    public bool useLocalModel = true;

    [Tooltip("Use this as deterministic fallback if model call fails.")]
    [TextArea(2, 5)]
    public string fallbackReply = "I am having trouble thinking right now, but I am still listening.";

    public bool HasRequiredReferences()
    {
        return personalityProfile != null
            && promptTemplate != null
            && memorySettings != null;
    }
}
