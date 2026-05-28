using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class LocalLlmClient
{
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
    }

    [Serializable]
    private class OllamaChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public bool stream;
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
        string jsonBody = BuildRequestJson(settings, endpoint, systemPrompt, playerInput);

        byte[] payload = Encoding.UTF8.GetBytes(jsonBody);
        using (var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, settings.timeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = string.Format("LLM request failed: {0}", request.error);
                onComplete?.Invoke(null, error);
                yield break;
            }

            string raw = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(raw))
            {
                onComplete?.Invoke(null, "LLM response was empty.");
                yield break;
            }

            string reply = ParseReply(endpoint, raw);
            if (string.IsNullOrWhiteSpace(reply))
            {
                onComplete?.Invoke(null, "LLM response could not be parsed.");
                yield break;
            }

            onComplete?.Invoke(SanitizeReply(reply), null);
        }
    }

    private static string BuildRequestJson(LocalModelSettings settings, string endpoint, string systemPrompt, string playerInput)
    {
        ChatMessage[] messages =
        {
            new ChatMessage { role = "system", content = systemPrompt },
            new ChatMessage { role = "user", content = playerInput }
        };

        if (IsOllamaNativeEndpoint(endpoint))
        {
            var ollamaRequest = new OllamaChatRequest
            {
                model = settings.modelName,
                messages = messages,
                stream = false,
                options = new OllamaOptions
                {
                    temperature = settings.temperature,
                    num_predict = settings.maxTokens
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

    private static string SanitizeReply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string cleaned = text.Trim();

        int thinkStart = cleaned.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        int thinkEnd = cleaned.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (thinkStart >= 0 && thinkEnd > thinkStart)
        {
            cleaned = cleaned.Remove(thinkStart, (thinkEnd + 8) - thinkStart).Trim();
        }

        return cleaned;
    }
}
