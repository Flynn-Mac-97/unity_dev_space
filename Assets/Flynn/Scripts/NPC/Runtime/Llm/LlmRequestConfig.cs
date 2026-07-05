

namespace Flynn.Npc
{
    public struct LlmRequestConfig
    {
        public string endpointUrl;
        public string modelName;
        public float temperature;
        public int maxTokens;
        public int timeoutSeconds;

        // Optional auth/headers (used by remote providers like OpenRouter).
        public string authorizationHeader; // e.g. "Bearer sk-or-..."
        public string httpReferer;
        public string xTitle;
        public bool forceJsonMode;
        public bool requireJsonProvider;
        public string proxyUrl;

        public bool IsValid => !string.IsNullOrWhiteSpace(endpointUrl) && !string.IsNullOrWhiteSpace(modelName);
    }

}
