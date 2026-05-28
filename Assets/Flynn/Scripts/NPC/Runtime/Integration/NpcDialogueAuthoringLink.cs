using UnityEngine;

public class NpcDialogueAuthoringLink : MonoBehaviour
{
    [Header("Authoring References")]
    public NpcDialogueAgentConfig agentConfig;
    public NpcDialogueData legacyDialogueData;

    [Header("Visual Targets")]
    public SpriteRenderer inGameSpriteRenderer;
    public SpriteRenderer portraitSpriteRenderer;
}
