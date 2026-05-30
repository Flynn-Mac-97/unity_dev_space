using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/NPC Memory Settings")]
public class NpcMemorySettings : ScriptableObject
{
    [Header("Per-Save Memory Limits")]
    [Tooltip("Maximum number of prior chat lines (one per speaker — so 8 = 4 player + 4 NPC) persisted and sent back to the model as context with every new turn.")]
    [Range(2, 40)]
    public int recentTurnsLimit = 8;

    [Tooltip("Maximum number of long-term facts about the player kept per NPC. Older facts drop off when the cap is exceeded.")]
    [Range(2, 30)]
    public int memoryFactsLimit = 10;

    [Tooltip("Maximum character length of a single stored fact. Longer extractions are truncated.")]
    [Range(40, 400)]
    public int maxFactLength = 160;
}
