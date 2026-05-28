using UnityEngine;

public class SceneLlmManager : MonoBehaviour
{
    public static SceneLlmManager Instance { get; private set; }

    [Header("Shared LLM Settings")]
    [Tooltip("All NPC dialogue agents in this scene use this model configuration.")]
    public LocalModelSettings sharedLocalModelSettings;

    [Tooltip("Optional global switch for dialogue systems that read this manager.")]
    public bool llmEnabled = true;

    [Header("Save Context")]
    [Tooltip("Active save slot identifier used by dialogue memory persistence.")]
    public string saveSlotId = "slot_0";

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
