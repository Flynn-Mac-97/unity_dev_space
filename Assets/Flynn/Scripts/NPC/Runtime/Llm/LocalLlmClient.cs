using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

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
        LocalModelSettings settings,
        string systemPrompt,
        IList<ChatTurn> priorTurns,
        string playerInput,
        Action<string, string> onComplete)
    {
        if (settings == null)
        {
            onComplete?.Invoke(null, "LocalModelSettings is null.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(settings.endpointUrl))
        {
            onComplete?.Invoke(null, "Endpoint URL is empty.");
            yield break;
        }

        string endpoint = settings.endpointUrl.Trim();
        string jsonBody = BuildRequestJson(settings, endpoint, systemPrompt, priorTurns, playerInput);

        Debug.Log("[LocalLlmClient] POST " + endpoint + " body=" + jsonBody);

        byte[] payload = Encoding.UTF8.GetBytes(jsonBody);
        using (var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, settings.timeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");

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

            Debug.Log("[LocalLlmClient] response=" + rawResponse);

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

    private static string BuildRequestJson(LocalModelSettings settings, string endpoint, string systemPrompt, IList<ChatTurn> priorTurns, string playerInput)
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
                model = settings.modelName,
                messages = messages,
                stream = false,
                think = false,
                keep_alive = "30m",
                options = new OllamaOptions
                {
                    temperature = settings.temperature,
                    num_predict = settings.maxTokens,
                    repeat_penalty = 1.15f
                }
            };

            return JsonUtility.ToJson(ollamaRequest);
        }

        var openAiRequest = new OpenAiChatRequest
        {
            model = settings.modelName,
            messages = messages,
            temperature = settings.temperature,
            max_tokens = settings.maxTokens
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
}
