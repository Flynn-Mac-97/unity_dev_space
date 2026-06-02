using System;
using System.Collections;

namespace Flynn.Npc.Memory
{
    // Turns text into a fixed-length embedding vector. Coroutine-based so the
    // default Ollama implementation can use UnityWebRequest like LocalLlmClient.
    // A future in-process SentisEmbeddingProvider (ONNX) implements the same
    // contract so callers never change.
    public interface IEmbeddingProvider
    {
        // Length of the vectors this provider returns (e.g. 384 for all-MiniLM-L6-v2).
        int Dimensions { get; }

        // Model identifier, stored in MetaDoc so a model swap can be detected.
        string ModelId { get; }

        // Embeds `text`. onComplete(vector, error): exactly one of the two is set.
        // vector is non-null on success; error is a human-readable string on failure.
        IEnumerator Embed(string text, Action<float[], string> onComplete);
    }
}
