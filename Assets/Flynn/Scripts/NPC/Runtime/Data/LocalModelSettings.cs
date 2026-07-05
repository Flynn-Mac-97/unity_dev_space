using UnityEngine;


using Flynn.UI.Core;

namespace Flynn.Npc
{
    [CreateAssetMenu(menuName = "Dialogue/Local Model Settings")]
    public class LocalModelSettings : ScriptableObject
    {
        [Header("Endpoint")]
        [Tooltip("Use either an OpenAI-compatible endpoint (for example Ollama /v1/chat/completions) or Ollama native /api/chat. Structured JSON output requires the native /api/chat endpoint.")]
        public string endpointUrl = "http://127.0.0.1:11434/api/chat";

        [Tooltip("Local model identifier exposed by your local server.")]
        public string modelName = "qwen35-test";

        [Header("Generation")]
        [Range(0f, 2f)]
        public float temperature = 0.7f;

        [Range(16, 4096)]
        public int maxTokens = 1024;

        [Range(1, 120)]
        public int timeoutSeconds = 45;

        [Header("Classifier (small model)")]
        [Tooltip("Smaller, faster model used for closed-vocabulary post-processing passes (topic, events, deltas). Same endpoint as the dialogue model.")]
        public string classifierModelName = "npc-qwen35-0.8b";

        [Range(0f, 2f)]
        public float classifierTemperature = 0.1f;

        [Range(8, 2048)]
        public int classifierMaxTokens = 512;
    }

}
