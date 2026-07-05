using UnityEngine;


using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc
{
    [CreateAssetMenu(menuName = "Dialogue/Remote Model Settings (OpenRouter)")]
    public class RemoteModelSettings : ScriptableObject
    {
        [Header("Endpoint")]
        [Tooltip("OpenAI-compatible chat completions URL. OpenRouter default shown.")]
        public string endpointUrl = "https://openrouter.ai/api/v1/chat/completions";

        [Tooltip("Model identifier on the provider, e.g. 'openai/gpt-4o-mini' or 'anthropic/claude-haiku-4.5'. Use the OpenRouter Settings window (Flynn → NPC → OpenRouter Settings) to browse and pick.")]
        public string modelName = "openai/gpt-4o-mini";

        [Header("Generation")]
        [Range(0f, 2f)] public float temperature = 0.7f;
        [Range(16, 8192)] public int maxTokens = 1024;
        [Range(1, 180)] public int timeoutSeconds = 60;

        [Tooltip("Send response_format={type:json_object}. Forces the provider to return valid JSON when the model supports JSON mode. Required for the NPC envelope contract.")]
        public bool forceJsonMode = true;

        [Tooltip("When forceJsonMode is on, also send provider.require_parameters=true. OpenRouter will then only route to backends that honor response_format — requests fail loudly instead of silently returning prose. Turn off if the chosen model has no JSON-capable provider.")]
        public bool requireJsonProvider = true;

        [Header("OpenRouter analytics (optional)")]
        [Tooltip("Sent as HTTP-Referer. OpenRouter uses this for per-app usage analytics. Safe to leave blank.")]
        public string httpReferer = "https://github.com/Flynn-Mac-97/unity_dev_space";

        [Tooltip("Sent as X-Title. Free-form app label shown in OpenRouter dashboard.")]
        public string xTitle = "Flynn NPC Sandbox";

        [Header("API key source")]
        [Tooltip("Editor: key is stored in EditorPrefs via the OpenRouter Settings window. Player builds fall back to the environment variable below.")]
        public string apiKeyEnvVar = "OPENROUTER_API_KEY";

        [Header("Proxy")]
        [Tooltip("HTTP(S) proxy URL for API requests, e.g. http://127.0.0.1:7890. Leave blank for direct connection.")]
        public string proxyUrl = "";
    }

}
