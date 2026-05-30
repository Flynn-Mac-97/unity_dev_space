using UnityEditor;
using UnityEngine;

public class NpcMemoryTabView
{
    private Vector2 _scroll;
    private Vector2 _turnsScroll;
    private Vector2 _factsScroll;
    private Vector2 _triggersScroll;

    private const float ListBoxHeight = 180f;

    public void Draw(NpcDialogueAgentConfig config)
    {
        if (config == null) return;

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Read-only view of what this NPC remembers from past conversations with the player. " +
            "Memory is stored per NPC in the active save slot. Memory limits (recent turns, fact count, fact length) " +
            "are configured on the SceneLlmManager and apply to all NPCs.",
            MessageType.Info);

        string saveSlotId = ResolveSaveSlot();
        string npcId = ResolveNpcId(config);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("LOOKUP", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("NPC ID", npcId);
        EditorGUILayout.LabelField("Save slot", saveSlotId);
        EditorGUILayout.EndVertical();

        var memory = NpcDialogueMemoryStore.GetOrCreateMemory(npcId, saveSlotId);
        bool empty = memory == null
            || ((memory.recentTurns == null || memory.recentTurns.Count == 0)
                && (memory.memoryFacts == null || memory.memoryFacts.Count == 0)
                && (memory.firedTriggers == null || memory.firedTriggers.Count == 0));

        if (empty)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox("No memory captured for this NPC in this save slot yet.", MessageType.None);
        }
        else
        {
            DrawList("Recent turns", memory != null ? memory.recentTurns : null, ref _turnsScroll);
            DrawList("Remembered facts about the player", memory != null ? memory.memoryFacts : null, ref _factsScroll);
            DrawList("Fired one-time triggers", memory != null ? memory.firedTriggers : null, ref _triggersScroll);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh"))
        {
            // No-op; IMGUI redraws every frame, but explicit button gives the
            // designer feedback that the view is live.
            GUI.FocusControl(null);
        }

        GUI.enabled = !empty;
        if (GUILayout.Button("Clear this NPC's memory in this slot"))
        {
            if (EditorUtility.DisplayDialog(
                "Clear NPC Memory",
                "Wipe stored turns, facts, and fired triggers for NPC '" + npcId + "' in slot '" + saveSlotId + "'? This cannot be undone.",
                "Clear", "Cancel"))
            {
                NpcDialogueMemoryStore.ClearNpcMemory(npcId, saveSlotId, true);
            }
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    private static void DrawList(string label, System.Collections.Generic.List<string> items, ref Vector2 scroll)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(label.ToUpperInvariant(), EditorStyles.boldLabel);

        int count = items != null ? items.Count : 0;
        EditorGUILayout.LabelField(count == 0 ? "(none)" : count + " entr" + (count == 1 ? "y" : "ies"),
            EditorStyles.miniLabel);

        if (count > 0)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(ListBoxHeight));
            for (int i = 0; i < items.Count; i++)
            {
                string line = items[i];
                if (string.IsNullOrEmpty(line)) continue;
                EditorGUILayout.SelectableLabel(
                    "• " + line,
                    EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight * Mathf.Max(1, EstimateLines(line))));
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    private static int EstimateLines(string line)
    {
        if (string.IsNullOrEmpty(line)) return 1;
        // Rough wrap estimate: ~90 chars per line at the typical tab width.
        return Mathf.Max(1, Mathf.CeilToInt(line.Length / 90f));
    }

    private static string ResolveNpcId(NpcDialogueAgentConfig config)
    {
        if (config != null && config.personalityProfile != null)
            return config.personalityProfile.GetSafeNpcId();
        return "npc.unknown";
    }

    private static string ResolveSaveSlot()
    {
        var manager = SceneLlmManager.Instance != null
            ? SceneLlmManager.Instance
            : Object.FindObjectOfType<SceneLlmManager>();
        if (manager != null && !string.IsNullOrWhiteSpace(manager.saveSlotId))
            return manager.saveSlotId;
        return "slot_0";
    }
}
