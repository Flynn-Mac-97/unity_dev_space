using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Local Model Settings")]
public class LocalModelSettings : ScriptableObject
{
    [Header("Endpoint")]
    [Tooltip("Use either an OpenAI-compatible endpoint (for example Ollama /v1/chat/completions) or Ollama native /api/chat.")]
    public string endpointUrl = "http://127.0.0.1:11434/v1/chat/completions";

    [Tooltip("Local model identifier exposed by your local server.")]
    public string modelName = "qwen3:4b-instruct";

    [Header("Generation")]
    [Range(0f, 2f)]
    public float temperature = 0.7f;

    [Range(16, 1024)]
    public int maxTokens = 180;

    [Range(1, 120)]
    public int timeoutSeconds = 25;
}
