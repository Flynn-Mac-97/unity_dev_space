using System;



namespace Flynn.Npc
{
    public static class OpenRouterApiKey
    {
        public const string EditorPrefsKey = "Flynn.OpenRouter.ApiKey";

        /// Resolve the key in this order: EditorPrefs (editor only) → env var (player or editor fallback).
        /// Returns null if nothing is configured.
        public static string Resolve(string envVarName)
        {
    #if UNITY_EDITOR
            string stored = UnityEditor.EditorPrefs.GetString(EditorPrefsKey, null);
            if (!string.IsNullOrWhiteSpace(stored)) return stored.Trim();
    #endif
            if (!string.IsNullOrWhiteSpace(envVarName))
            {
                string env = System.Environment.GetEnvironmentVariable(envVarName);
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
            }
            return null;
        }
    }

}
