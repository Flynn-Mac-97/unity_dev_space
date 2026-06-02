using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// One-time migration tool: pull the legacy npc_dialogue_memory_<slot>.json file
// from persistentDataPath into a fresh NpcMemoryStore SO. Safe to run multiple
// times — overwrites the SO's contents with whatever's on disk.
public static class NpcMemoryStoreMigration
{
    [Serializable]
    private class LegacyEntry
    {
        public string npcId;
        public List<string> recentTurns;
        public List<string> memoryFacts;
        public List<string> firedTriggers;
    }

    [Serializable]
    private class LegacySaveFile
    {
        public List<LegacyEntry> entries = new List<LegacyEntry>();
    }

    public struct Result
    {
        public bool fileFound;
        public int npcCount;
        public int turnCount;
        public int factCount;
        public string sourcePath;
    }

    public static Result MigrateJsonToStore(NpcMemoryStore store, string slotId)
    {
        var r = new Result();
        if (store == null) return r;

        string fileName = "npc_dialogue_memory_" + (string.IsNullOrWhiteSpace(slotId) ? "slot_0" : slotId) + ".json";
        string path = Path.Combine(Application.persistentDataPath, fileName);
        r.sourcePath = path;
        if (!File.Exists(path)) return r;
        r.fileFound = true;

        string raw = File.ReadAllText(path);
        var save = JsonUtility.FromJson<LegacySaveFile>(raw);
        if (save == null || save.entries == null) return r;

        foreach (var e in save.entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.npcId)) continue;
            var target = store.GetOrCreate(e.npcId);
            target.recentTurns.Clear();
            target.memoryFacts.Clear();
            target.firedTriggers.Clear();
            if (e.recentTurns   != null) { target.recentTurns.AddRange(e.recentTurns);     r.turnCount += e.recentTurns.Count; }
            if (e.memoryFacts   != null) { target.memoryFacts.AddRange(e.memoryFacts);     r.factCount += e.memoryFacts.Count; }
            if (e.firedTriggers != null) { target.firedTriggers.AddRange(e.firedTriggers); }
            r.npcCount++;
        }

        EditorUtility.SetDirty(store);
        AssetDatabase.SaveAssets();
        return r;
    }
}
