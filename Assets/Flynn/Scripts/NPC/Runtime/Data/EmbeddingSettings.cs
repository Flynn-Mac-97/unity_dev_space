using UnityEngine;


using Flynn.UI.Core;

namespace Flynn.Npc
{
    // Designer-facing settings for the embedding model used by semantic memory
    // recall. Mirrors LocalModelSettings but for the /api/embeddings endpoint.
    // Served by Ollama: `ollama pull all-minilm` (all-MiniLM-L6-v2, 384-dim).
    [CreateAssetMenu(menuName = "Dialogue/Embedding Settings", fileName = "EmbeddingSettings")]
    public class EmbeddingSettings : ScriptableObject
    {
        [Header("Endpoint")]
        [Tooltip("Ollama embeddings endpoint. Default is the native /api/embeddings route.")]
        public string endpointUrl = "http://127.0.0.1:11434/api/embeddings";

        [Tooltip("Embedding model identifier exposed by your local server, including the tag. `ollama pull all-minilm` installs the tag 'all-minilm:l6-v2'.")]
        public string modelName = "all-minilm:l6-v2";

        [Tooltip("Vector length the model returns. all-MiniLM-L6-v2 = 384. Used for validation only.")]
        public int dimensions = 384;

        [Range(1, 60)]
        [Tooltip("Per-request timeout for an embedding call.")]
        public int timeoutSeconds = 20;

        [Header("Recall Budget")]
        [Range(1, 20)]
        [Tooltip("Max memories + knowledge items injected into the prompt per turn (top-k by blended score).")]
        public int recallTopK = 6;

        [Range(0f, 1f)]
        [Tooltip("Minimum cosine similarity for a memory/knowledge item to be eligible for recall. Filters noise.")]
        public float minSimilarity = 0.2f;

        [Range(0.5f, 1f)]
        [Tooltip("Cosine threshold above which a new fact is treated as a duplicate of an existing one and skipped on write.")]
        public float dedupThreshold = 0.95f;
    }

}
