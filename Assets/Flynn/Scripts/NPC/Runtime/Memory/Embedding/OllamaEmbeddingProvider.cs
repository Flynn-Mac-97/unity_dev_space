using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

using Flynn.Npc;

namespace Flynn.Npc.Memory
{
    // Embedding provider backed by Ollama's /api/embeddings endpoint. Mirrors the
    // UnityWebRequest pattern in LocalLlmClient. Request:  {model, prompt}.
    // Response: {"embedding":[...]}.
    public class OllamaEmbeddingProvider
    {
        private readonly string _endpoint;
        private readonly string _model;
        private readonly int _dimensions;
        private readonly int _timeoutSeconds;

        public int Dimensions => _dimensions;
        public string ModelId => _model;

        public OllamaEmbeddingProvider(EmbeddingSettings settings)
        {
            _endpoint = settings != null && !string.IsNullOrWhiteSpace(settings.endpointUrl)
                ? settings.endpointUrl.Trim()
                : "http://127.0.0.1:11434/api/embeddings";
            _model = settings != null && !string.IsNullOrWhiteSpace(settings.modelName)
                ? settings.modelName.Trim()
                : "all-minilm";
            _dimensions = settings != null ? Mathf.Max(1, settings.dimensions) : 384;
            _timeoutSeconds = settings != null ? Mathf.Clamp(settings.timeoutSeconds, 1, 120) : 20;
        }

        [Serializable]
        private class EmbedRequest
        {
            public string model;
            public string prompt;
        }

        [Serializable]
        private class EmbedResponse
        {
            public float[] embedding;
        }

        public IEnumerator Embed(string text, Action<float[], string> onComplete)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                onComplete?.Invoke(null, "Cannot embed empty text.");
                yield break;
            }

            string body = JsonUtility.ToJson(new EmbedRequest { model = _model, prompt = text });
            byte[] payload = Encoding.UTF8.GetBytes(body);

            using (var request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = _timeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(null, string.Format(
                        "Embedding request failed: {0} (HTTP {1})", request.error, request.responseCode));
                    yield break;
                }

                string raw = request.downloadHandler != null ? request.downloadHandler.text : null;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    onComplete?.Invoke(null, "Embedding response was empty.");
                    yield break;
                }

                EmbedResponse parsed = null;
                try { parsed = JsonUtility.FromJson<EmbedResponse>(raw); }
                catch (Exception e) { onComplete?.Invoke(null, "Embedding parse failed: " + e.Message); yield break; }

                if (parsed == null || parsed.embedding == null || parsed.embedding.Length == 0)
                {
                    onComplete?.Invoke(null, "Embedding response had no vector. body=" + raw);
                    yield break;
                }

                if (parsed.embedding.Length != _dimensions)
                    Debug.LogWarning(string.Format(
                        "[OllamaEmbeddingProvider] Expected {0}-dim vector, got {1}. Using it anyway; check EmbeddingSettings.dimensions / model '{2}'.",
                        _dimensions, parsed.embedding.Length, _model));

                onComplete?.Invoke(parsed.embedding, null);
            }
        }
    }
}
