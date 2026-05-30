using UnityEngine;

public class SceneLlmManager : MonoBehaviour
{
    public static SceneLlmManager Instance { get; private set; }

    [Header("Shared LLM Settings")]
    [Tooltip("All NPC dialogue agents in this scene use this model configuration.")]
    public LocalModelSettings sharedLocalModelSettings;

    [Tooltip("Optional global switch for dialogue systems that read this manager.")]
    public bool llmEnabled = true;

    [Header("Shared Memory Settings")]
    [Tooltip("Memory budget used by every NPC in this scene — chat history length, persisted fact count, prompt injection size.")]
    public NpcMemorySettings sharedMemorySettings;

    [Header("Shared Capability Library")]
    [Tooltip("Master list of in-game capabilities NPCs may be assigned. Each NPC's Identity tab picks from this list.")]
    public NpcCapabilityLibrary capabilityLibrary;

    [Header("Save Context")]
    [Tooltip("Active save slot identifier used by dialogue memory persistence.")]
    public string saveSlotId = "slot_0";

    [Header("Player")]
    [Tooltip("Profile describing who the player character is. Used by NPCs to write player dialogue options in the right voice.")]
    public PlayerDialogueProfile playerProfile;

    [Header("Global System Prompt")]
    [Tooltip("Global system prompt prepended to every NPC's persona. Put rules here about what the model is, how it should respond, output style, length, and what to avoid (e.g. 'never admit to being an AI'). Per-NPC persona text is appended after this.")]
    [TextArea(6, 30)]
    public string systemPrompt =
        "You are a character in a 2.5D solarpunk adventure game. " +
        "Reply in plain text, in character, in 1-3 short lines. " +
        "Stay in voice. Never claim to be an AI or language model. " +
        "If the player brings up a topic you avoid, deflect briefly.";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple SceneLlmManager instances detected. The latest instance will be used.");
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool HasValidSettings()
    {
        return llmEnabled && sharedLocalModelSettings != null;
    }
}
