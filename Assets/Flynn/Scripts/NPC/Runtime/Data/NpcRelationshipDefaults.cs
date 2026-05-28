using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/NPC Relationship Defaults", fileName = "NpcRelationshipDefaults")]
public class NpcRelationshipDefaults : ScriptableObject
{
    public enum PlayerStatusTag
    {
        Unknown,
        Naive,
        Useful,
        Dangerous,
        Important,
    }

    [Header("Starting relationship (0 - 100)")]
    [Range(0, 100)] public int startingTrust = 25;
    [Range(0, 100)] public int startingAffection = 25;
    [Range(0, 100)] public int startingSuspicion = 25;

    [Header("Initial perception")]
    [Tooltip("How this NPC initially sizes up the player. Used to color tone before any interaction history.")]
    public PlayerStatusTag initialPlayerStatus = PlayerStatusTag.Unknown;

    [Header("Reveal thresholds")]
    [Tooltip("Trust required before this NPC volunteers entries flagged TrustAtLeast in the knowledge base.")]
    [Range(0, 100)] public int trustToShareClues = 50;

    [Tooltip("Trust required before this NPC will reveal Secret entries.")]
    [Range(0, 100)] public int trustToShareSecrets = 75;
}
