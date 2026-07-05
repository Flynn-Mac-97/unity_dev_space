using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

using Flynn.Npc;

namespace Flynn.Npc.Editor
{
    public class OpenRouterSettingsWindow : EditorWindow
    {
        private const string ModelsUrl = "https://openrouter.ai/api/v1/models";
        private const string AuthKeyUrl = "https://openrouter.ai/api/v1/auth/key";

        [Serializable] private class ModelEntry { public string id; public string name; public string description; }
        [Serializable] private class ModelListResponse { public ModelEntry[] data; }

        private string _apiKey = "";
        private RemoteModelSettings _target;
        private List<ModelEntry> _models = new List<ModelEntry>();
        private string[] _modelIds = Array.Empty<string>();
        private string _search = "";
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.None;
        private UnityWebRequestAsyncOperation _inflight;
        private Vector2 _scroll;
        private bool _fetchingModels;

        [MenuItem("Flynn/NPC/OpenRouter Settings")]
        public static void ShowWindow()
        {
            var w = GetWindow<OpenRouterSettingsWindow>(true, "OpenRouter Settings", true);
            w.minSize = new Vector2(520, 520);
            w.Show();
        }

        private void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(OpenRouterApiKey.EditorPrefsKey, "");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("OpenRouter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "API key is stored in EditorPrefs on this machine (key: " + OpenRouterApiKey.EditorPrefsKey + "). " +
                "Player builds fall back to the env var configured on RemoteModelSettings.",
                MessageType.None);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("API Key", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _apiKey = EditorGUILayout.PasswordField(_apiKey);
                if (GUILayout.Button("Save", GUILayout.Width(60)))
                {
                    EditorPrefs.SetString(OpenRouterApiKey.EditorPrefsKey, _apiKey?.Trim() ?? "");
                    SetStatus("API key saved to EditorPrefs.", MessageType.Info);
                }
                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    _apiKey = "";
                    EditorPrefs.DeleteKey(OpenRouterApiKey.EditorPrefsKey);
                    SetStatus("API key cleared.", MessageType.Info);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !_fetchingModels;
                if (GUILayout.Button("Test Connection"))
                    TestConnection();
                if (GUILayout.Button("Fetch Model List"))
                    FetchModels();
                GUI.enabled = true;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Target asset", EditorStyles.miniBoldLabel);
            _target = (RemoteModelSettings)EditorGUILayout.ObjectField(
                "RemoteModelSettings", _target, typeof(RemoteModelSettings), false);

            if (_target == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or create a RemoteModelSettings asset to write model selections into. " +
                    "Create one via: Project → right-click → Create → Dialogue → Remote Model Settings (OpenRouter).",
                    MessageType.Info);
            }
            else
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Current dialogue model", _target.modelName);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Models", EditorStyles.boldLabel);
            if (_models.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Fetch Model List' to load available models from OpenRouter.", MessageType.None);
            }
            else
            {
                _search = EditorGUILayout.TextField("Filter", _search);
                var filtered = string.IsNullOrWhiteSpace(_search)
                    ? _models
                    : _models.Where(m =>
                        (m.id != null && m.id.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (m.name != null && m.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

                EditorGUILayout.LabelField($"{filtered.Count} / {_models.Count} models");
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
                foreach (var m in filtered)
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField(m.id, EditorStyles.boldLabel);
                            if (!string.IsNullOrEmpty(m.name) && m.name != m.id)
                                EditorGUILayout.LabelField(m.name, EditorStyles.miniLabel);
                        }
                        GUI.enabled = _target != null;
                        if (GUILayout.Button("Use as Dialogue", GUILayout.Width(120)))
                            AssignModel(m.id);
                        GUI.enabled = true;
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }
        }

        private void AssignModel(string id)
        {
            if (_target == null) return;
            Undo.RecordObject(_target, "Assign OpenRouter model");
            _target.modelName = id;
            EditorUtility.SetDirty(_target);
            SetStatus($"Set dialogue model to {id}.", MessageType.Info);
        }

        private void TestConnection()
        {
            string key = (_apiKey ?? "").Trim();
            if (string.IsNullOrEmpty(key))
            {
                SetStatus("Save an API key first.", MessageType.Warning);
                return;
            }
            var req = UnityWebRequest.Get(AuthKeyUrl);
            req.SetRequestHeader("Authorization", "Bearer " + key);
            SetStatus("Testing connection…", MessageType.None);
            _fetchingModels = true;
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                _fetchingModels = false;
                if (req.result == UnityWebRequest.Result.Success)
                    SetStatus("Connection OK. Response: " + req.downloadHandler.text, MessageType.Info);
                else
                    SetStatus($"Connection failed ({req.responseCode}): {req.error}\n{req.downloadHandler?.text}", MessageType.Error);
                req.Dispose();
                Repaint();
            };
        }

        private void FetchModels()
        {
            var req = UnityWebRequest.Get(ModelsUrl);
            string key = (_apiKey ?? "").Trim();
            if (!string.IsNullOrEmpty(key))
                req.SetRequestHeader("Authorization", "Bearer " + key);

            SetStatus("Fetching model list…", MessageType.None);
            _fetchingModels = true;
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                _fetchingModels = false;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    SetStatus($"Fetch failed ({req.responseCode}): {req.error}\n{req.downloadHandler?.text}", MessageType.Error);
                    req.Dispose();
                    Repaint();
                    return;
                }

                try
                {
                    var parsed = JsonUtility.FromJson<ModelListResponse>(req.downloadHandler.text);
                    _models = parsed?.data != null
                        ? parsed.data.OrderBy(m => m.id, StringComparer.OrdinalIgnoreCase).ToList()
                        : new List<ModelEntry>();
                    _modelIds = _models.Select(m => m.id).ToArray();
                    SetStatus($"Loaded {_models.Count} models.", MessageType.Info);
                }
                catch (Exception e)
                {
                    SetStatus("Parse error: " + e.Message, MessageType.Error);
                }
                req.Dispose();
                Repaint();
            };
        }

        private void SetStatus(string msg, MessageType type)
        {
            _statusMessage = msg;
            _statusType = type;
            Repaint();
        }
    }
}
