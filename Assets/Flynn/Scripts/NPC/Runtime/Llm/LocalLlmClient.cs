using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Npc
{
public static class LocalLlmClient
{
    public struct ChatTurn
    {
        public bool isAssistant;
        public string content;
    }

    [Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class OpenAiChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public float temperature;
        public int max_tokens;
    }

    [Serializable]
    private class ResponseFormat
    {
        public string type;
    }

    [Serializable]
    private class OpenAiChatRequestJsonMode
    {
        public string model;
        public ChatMessage[] messages;
        public float temperature;
        public int max_tokens;
        public ResponseFormat response_format;
    }

    [Serializable]
    private class ProviderRouting
    {
        public bool require_parameters;
    }

    [Serializable]
    private class OpenAiChatRequestJsonModeStrict
    {
        public string model;
        public ChatMessage[] messages;
        public float temperature;
        public int max_tokens;
        public ResponseFormat response_format;
        public ProviderRouting provider;
    }

    [Serializable]
    private class OpenAiChoice
    {
        public ChatMessage message;
    }

    [Serializable]
    private class OpenAiChatResponse
    {
        public OpenAiChoice[] choices;
    }

    [Serializable]
    private class OllamaOptions
    {
        public float temperature;
        public int num_predict;
        public float repeat_penalty;
    }

    [Serializable]
    private class OllamaChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public bool stream;
        public bool think;
        public string keep_alive;
        public OllamaOptions options;
    }

    [Serializable]
    private class OllamaChatResponse
    {
        public ChatMessage message;
    }

    public static IEnumerator GenerateReply(
        LlmRequestConfig config,
        string systemPrompt,
        IList<ChatTurn> priorTurns,
        string playerInput,
        Action<string, string> onComplete)
    {
        if (!config.IsValid)
        {
            onComplete?.Invoke(null, "LLM request config is invalid (missing endpoint or model name).");
            yield break;
        }

        string endpoint = config.endpointUrl.Trim();
        string jsonBody = BuildRequestJson(config, endpoint, systemPrompt, priorTurns, playerInput);

        Debug.Log("[LocalLlmClient] POST " + endpoint + " body=" + jsonBody);

        byte[] payload = Encoding.UTF8.GetBytes(jsonBody);
        using (var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.Max(1, config.timeoutSeconds);

                if (!string.IsNullOrWhiteSpace(config.proxyUrl))
                    ConfigureProxy(config.proxyUrl);

                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrWhiteSpace(config.authorizationHeader))
                    request.SetRequestHeader("Authorization", config.authorizationHeader);
                if (!string.IsNullOrWhiteSpace(config.httpReferer))
                    request.SetRequestHeader("HTTP-Referer", config.httpReferer);
                if (!string.IsNullOrWhiteSpace(config.xTitle))
                    request.SetRequestHeader("X-Title", config.xTitle);

                yield return request.SendWebRequest();

                string rawResponse = request.downloadHandler != null ? request.downloadHandler.text : null;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string error = string.Format("LLM request failed: {0} (HTTP {1}) body='{2}'",
                        request.error, request.responseCode, rawResponse);
                    onComplete?.Invoke(null, error);
                    yield break;
                }

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    onComplete?.Invoke(null, "LLM response was empty.");
                    yield break;
                }

                Debug.Log("[LocalLlmClient] response=" + (rawResponse ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n"));

                string reply = ParseReply(endpoint, rawResponse);
                if (reply == null)
                {
                    // Genuine parse failure — the JSON was malformed or missing expected fields.
                    onComplete?.Invoke(null, "LLM response could not be parsed. body='" + rawResponse + "'");
                    yield break;
                }

                // Empty content is expected when the <think> stop sequence fires: the model
                // started thinking before replying and was halted immediately. Route through
                // as a null reply so HandleAgentTurn falls back gracefully without logging
                // a spurious error.
                string sanitized = SanitizeReply(reply);
                onComplete?.Invoke(string.IsNullOrWhiteSpace(sanitized) ? null : sanitized, null);
            }
        }

        private static string BuildRequestJson(LlmRequestConfig config, string endpoint, string systemPrompt, IList<ChatTurn> priorTurns, string playerInput)
        {
            var msgList = new List<ChatMessage>(4 + (priorTurns != null ? priorTurns.Count : 0));
            msgList.Add(new ChatMessage { role = "system", content = systemPrompt });

            if (priorTurns != null)
            {
                for (int i = 0; i < priorTurns.Count; i++)
                {
                    var t = priorTurns[i];
                    if (string.IsNullOrWhiteSpace(t.content)) continue;
                    msgList.Add(new ChatMessage
                    {
                        role = t.isAssistant ? "assistant" : "user",
                        content = t.content
                    });
                }
            }

            msgList.Add(new ChatMessage { role = "user", content = playerInput ?? string.Empty });
            ChatMessage[] messages = msgList.ToArray();

            if (IsOllamaNativeEndpoint(endpoint))
            {
                var ollamaRequest = new OllamaChatRequest
                {
                    model = config.modelName,
                    messages = messages,
                    stream = false,
                    think = false,
                    keep_alive = "30m",
                    options = new OllamaOptions
                    {
                        temperature = config.temperature,
                        num_predict = config.maxTokens,
                        repeat_penalty = 1.15f
                    }
                };

                return JsonUtility.ToJson(ollamaRequest);
            }

            if (config.forceJsonMode)
            {
                if (config.requireJsonProvider)
                {
                    var strict = new OpenAiChatRequestJsonModeStrict
                    {
                        model = config.modelName,
                        messages = messages,
                        temperature = config.temperature,
                        max_tokens = config.maxTokens,
                        response_format = new ResponseFormat { type = "json_object" },
                        provider = new ProviderRouting { require_parameters = true }
                    };
                    return JsonUtility.ToJson(strict);
                }

                var jsonModeRequest = new OpenAiChatRequestJsonMode
                {
                    model = config.modelName,
                    messages = messages,
                    temperature = config.temperature,
                    max_tokens = config.maxTokens,
                    response_format = new ResponseFormat { type = "json_object" }
                };
                return JsonUtility.ToJson(jsonModeRequest);
            }

            var openAiRequest = new OpenAiChatRequest
            {
                model = config.modelName,
                messages = messages,
                temperature = config.temperature,
                max_tokens = config.maxTokens
            };

            return JsonUtility.ToJson(openAiRequest);
        }

        private static string ParseReply(string endpoint, string raw)
        {
            if (IsOllamaNativeEndpoint(endpoint))
            {
                var ollamaResponse = JsonUtility.FromJson<OllamaChatResponse>(raw);
                return ollamaResponse != null && ollamaResponse.message != null
                    ? ollamaResponse.message.content
                    : null;
            }

            var openAiResponse = JsonUtility.FromJson<OpenAiChatResponse>(raw);
            if (openAiResponse == null || openAiResponse.choices == null || openAiResponse.choices.Length == 0)
                return null;

            var message = openAiResponse.choices[0].message;
            return message != null ? message.content : null;
        }

        private static bool IsOllamaNativeEndpoint(string endpoint)
        {
            return endpoint.IndexOf("/api/chat", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string SanitizeReply(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string cleaned = text.Trim();

            // Strip every closed <think>...</think> block (case-insensitive, including newlines).
            while (true)
            {
                int s = cleaned.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                if (s < 0) break;
                int e = cleaned.IndexOf("</think>", s, StringComparison.OrdinalIgnoreCase);
                if (e < 0)
                {
                    // Unclosed think — model ran out of budget mid-thought. Drop everything from
                    // the opening tag onward so it never reaches the player.
                    cleaned = cleaned.Substring(0, s);
                    break;
                }
                cleaned = cleaned.Remove(s, (e + 8) - s);
            }

            return cleaned.Trim();
        }

        private static void ConfigureProxy(string proxyUrl)
        {
            try
            {
                var uri = new Uri(proxyUrl.Trim());
                WebRequest.DefaultWebProxy = new WebProxy(uri) { BypassProxyOnLocal = true };
                Debug.Log("[LocalLlmClient] Proxy set to " + uri);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LocalLlmClient] Failed to configure proxy '" + proxyUrl + "': " + e.Message);
            }
        }
    }
}
