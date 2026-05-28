using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/NPC Memory Settings")]
public class NpcMemorySettings : ScriptableObject
{
    [Header("Per-Save Memory Limits")]
    [Range(2, 20)]
    public int recentTurnsLimit = 8;

    [Range(2, 30)]
    public int memoryFactsLimit = 10;

    [Range(40, 400)]
    public int maxFactLength = 160;

    [Header("Prompt Injection Budget")]
    [Range(1, 10)]
    public int injectedRecentTurns = 4;

    [Range(1, 10)]
    public int injectedFacts = 4;
}
