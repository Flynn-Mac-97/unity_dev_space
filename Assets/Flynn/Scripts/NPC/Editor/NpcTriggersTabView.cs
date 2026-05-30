using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NpcTriggersTabView
{
    private const string TriggersFolder = "Assets/Flynn/Configs/NPC/Triggers";

    private Vector2 _scroll;
    private SerializedObject _configSo;
    private ReorderableList _list;
    private NpcDialogueAgentConfig _boundConfig;

    public void Draw(NpcDialogueAgentConfig config)
    {
        if (config == null) return;

        EnsureBindings(config);

        _configSo.Update();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Triggers are DialogueTriggerDef assets the NPC may fire mid-dialogue. " +
            "Drag the same def into a DialogueTriggerListener in your scene to wire a reaction.",
            MessageType.None);

        _list.DoLayoutList();

        EditorGUILayout.Space(8);
        DrawCreateTriggerButton(config);

        _configSo.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();
    }

    private void EnsureBindings(NpcDialogueAgentConfig config)
    {
        if (_configSo != null && _boundConfig == config) return;

        _boundConfig = config;
        _configSo = new SerializedObject(config);
        var arr = _configSo.FindProperty("triggers");

        _list = new ReorderableList(_configSo, arr, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "DIALOGUE TRIGGERS", EditorStyles.boldLabel),
            elementHeightCallback = i => GetRowHeight(arr.GetArrayElementAtIndex(i)),
            drawElementCallback = (rect, i, active, focused) => DrawRow(rect, arr.GetArrayElementAtIndex(i)),
        };
    }

    private static float GetRowHeight(SerializedProperty element)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float pad = EditorGUIUtility.standardVerticalSpacing;

        // ref field + description preview + listener-count line + action button
        return (line + pad) * 4 + 8f;
    }

    private static void DrawRow(Rect rect, SerializedProperty element)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float pad = EditorGUIUtility.standardVerticalSpacing;
        float y = rect.y + 2f;

        // Field 1: the def reference.
        var defRect = new Rect(rect.x, y, rect.width, line);
        EditorGUI.PropertyField(defRect, element, new GUIContent("Trigger"));
        y += line + pad;

        var def = element.objectReferenceValue as DialogueTriggerDef;
        if (def == null)
        {
            EditorGUI.LabelField(new Rect(rect.x, y, rect.width, line),
                "Drop a DialogueTriggerDef asset, or use the button below to create one.",
                EditorStyles.miniLabel);
            return;
        }

        // Field 2: description preview (read-only, click to ping the def asset).
        string desc = string.IsNullOrWhiteSpace(def.description) ? "(no description)" : def.description.Trim();
        string label = string.Format("{0} — {1}", def.kind, desc);
        EditorGUI.LabelField(new Rect(rect.x, y, rect.width, line), label, EditorStyles.miniLabel);
        y += line + pad;

        // Field 3: listener discovery.
        var listeners = FindSceneListeners(def);
        var countRect = new Rect(rect.x, y, rect.width - 80f, line);
        EditorGUI.LabelField(countRect,
            string.Format("Scene listeners: {0}", listeners.Count),
            listeners.Count > 0 ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);

        var pingRect = new Rect(rect.x + rect.width - 78f, y, 78f, line);
        using (new EditorGUI.DisabledScope(listeners.Count == 0))
        {
            if (GUI.Button(pingRect, "Ping"))
            {
                Selection.objects = listeners.ConvertAll(l => (Object)l.gameObject).ToArray();
                if (listeners.Count > 0) EditorGUIUtility.PingObject(listeners[0]);
            }
        }
        y += line + pad;

        // Field 4: spawn listener.
        var spawnRect = new Rect(rect.x, y, rect.width, line);
        if (GUI.Button(spawnRect, "+ Add scene listener for this trigger"))
            SpawnListenerInActiveScene(def);
    }

    private void DrawCreateTriggerButton(NpcDialogueAgentConfig config)
    {
        if (!GUILayout.Button("Create new DialogueTriggerDef asset…")) return;

        EnsureFolder(TriggersFolder);
        string suggested = string.IsNullOrEmpty(config.name) ? "trigger_new" : "trigger." + config.name.ToLowerInvariant() + ".new";
        string path = AssetDatabase.GenerateUniqueAssetPath(TriggersFolder + "/" + suggested + ".asset");

        var def = ScriptableObject.CreateInstance<DialogueTriggerDef>();
        AssetDatabase.CreateAsset(def, path);
        AssetDatabase.SaveAssets();

        // Append to the bound list.
        _configSo.Update();
        var arr = _configSo.FindProperty("triggers");
        arr.arraySize++;
        arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = def;
        _configSo.ApplyModifiedProperties();

        Selection.activeObject = def;
        EditorGUIUtility.PingObject(def);
    }

    private static void SpawnListenerInActiveScene(DialogueTriggerDef def)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[DialogueTriggers] No active scene to spawn listener in.");
            return;
        }

        var channel = FindFirstTriggerChannel();
        var go = new GameObject("DialogueListener_" + def.name);
        SceneManager.MoveGameObjectToScene(go, scene);

        var listener = go.AddComponent<DialogueTriggerListener>();
        listener.channel = channel;
        listener.requiredTrigger = def;

        Undo.RegisterCreatedObjectUndo(go, "Add Dialogue Trigger Listener");
        EditorSceneManager.MarkSceneDirty(scene);

        Selection.activeObject = go;
        EditorGUIUtility.PingObject(go);
    }

    private static DialogueTriggerChannel FindFirstTriggerChannel()
    {
        string[] guids = AssetDatabase.FindAssets("t:DialogueTriggerChannel");
        if (guids == null || guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<DialogueTriggerChannel>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static List<DialogueTriggerListener> FindSceneListeners(DialogueTriggerDef def)
    {
        var result = new List<DialogueTriggerListener>();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentsInChildren<DialogueTriggerListener>(true);
                for (int j = 0; j < found.Length; j++)
                    if (found[j].requiredTrigger == def) result.Add(found[j]);
            }
        }
        return result;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string[] parts = folder.Split('/');
        string running = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = running + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(running, parts[i]);
            running = next;
        }
    }
}
