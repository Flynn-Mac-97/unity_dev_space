using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class NpcDialogueMemoryStore
{
    [Serializable]
    public class NpcMemoryEntry
    {
        public string npcId;
        public List<string> recentTurns = new List<string>();
        public List<string> memoryFacts = new List<string>();
    }

    [Serializable]
    private class NpcMemorySaveFile
    {
        public List<NpcMemoryEntry> entries = new List<NpcMemoryEntry>();
    }

    private const string k_DefaultSaveSlotId = "slot_0";
    private const string k_FilePrefix = "npc_dialogue_memory_";

    private static readonly Dictionary<string, NpcMemorySaveFile> s_cacheBySlot =
        new Dictionary<string, NpcMemorySaveFile>(StringComparer.OrdinalIgnoreCase);

    public static NpcMemoryEntry GetOrCreateMemory(string npcId, string saveSlotId)
    {
        string safeNpcId = GetSafeNpcId(npcId);
        string safeSlotId = GetSafeSaveSlotId(saveSlotId);
        NpcMemorySaveFile saveFile = GetOrLoadSaveFile(safeSlotId);

        for (int i = 0; i < saveFile.entries.Count; i++)
        {
            if (string.Equals(saveFile.entries[i].npcId, safeNpcId, StringComparison.OrdinalIgnoreCase))
            {
                EnsureEntryLists(saveFile.entries[i]);
                return saveFile.entries[i];
            }
        }

        NpcMemoryEntry created = new NpcMemoryEntry { npcId = safeNpcId };
        saveFile.entries.Add(created);
        return created;
    }

    public static void AddTurn(string npcId, string speaker, string content, int recentTurnsLimit, string saveSlotId)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        NpcMemoryEntry entry = GetOrCreateMemory(npcId, saveSlotId);
        string safeSpeaker = string.IsNullOrWhiteSpace(speaker) ? "Unknown" : speaker.Trim();
        entry.recentTurns.Add(string.Format("{0}: {1}", safeSpeaker, content.Trim()));
        TrimList(entry.recentTurns, Mathf.Max(2, recentTurnsLimit));
    }

    public static void AddFact(string npcId, string fact, int factsLimit, int maxFactLength, string saveSlotId)
    {
        if (string.IsNullOrWhiteSpace(fact)) return;

        NpcMemoryEntry entry = GetOrCreateMemory(npcId, saveSlotId);
        string trimmed = fact.Trim();
        if (maxFactLength > 0 && trimmed.Length > maxFactLength)
            trimmed = trimmed.Substring(0, maxFactLength);

        if (string.IsNullOrWhiteSpace(trimmed)) return;

        for (int i = 0; i < entry.memoryFacts.Count; i++)
        {
            if (string.Equals(entry.memoryFacts[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                Save(saveSlotId);
                return;
            }
        }

        entry.memoryFacts.Add(trimmed);
        TrimList(entry.memoryFacts, Mathf.Max(1, factsLimit));
    }

    public static bool ClearNpcMemory(string npcId, string saveSlotId, bool saveToDisk)
    {
        string safeNpcId = GetSafeNpcId(npcId);
        string safeSlotId = GetSafeSaveSlotId(saveSlotId);
        NpcMemorySaveFile saveFile = GetOrLoadSaveFile(safeSlotId);

        bool removed = false;
        for (int i = saveFile.entries.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(saveFile.entries[i].npcId, safeNpcId, StringComparison.OrdinalIgnoreCase))
                continue;

            saveFile.entries.RemoveAt(i);
            removed = true;
        }

        if (saveToDisk)
            Save(safeSlotId);

        return removed;
    }

    public static void Save(string saveSlotId)
    {
        string safeSlotId = GetSafeSaveSlotId(saveSlotId);
        NpcMemorySaveFile saveFile = GetOrLoadSaveFile(safeSlotId);
        string path = GetFilePath(safeSlotId);

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(saveFile, true);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(string.Format("[DialogueMemory] Failed to save memory file '{0}'. {1}", path, ex.Message));
        }
    }

    private static NpcMemorySaveFile GetOrLoadSaveFile(string saveSlotId)
    {
        string safeSlotId = GetSafeSaveSlotId(saveSlotId);
        NpcMemorySaveFile loaded;
        if (s_cacheBySlot.TryGetValue(safeSlotId, out loaded))
            return loaded;

        string path = GetFilePath(safeSlotId);
        NpcMemorySaveFile file = null;

        if (File.Exists(path))
        {
            try
            {
                string raw = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(raw))
                    file = JsonUtility.FromJson<NpcMemorySaveFile>(raw);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(string.Format("[DialogueMemory] Failed to load memory file '{0}'. {1}", path, ex.Message));
            }
        }

        if (file == null)
            file = new NpcMemorySaveFile();

        if (file.entries == null)
            file.entries = new List<NpcMemoryEntry>();

        for (int i = 0; i < file.entries.Count; i++)
            EnsureEntryLists(file.entries[i]);

        s_cacheBySlot[safeSlotId] = file;
        return file;
    }

    private static void EnsureEntryLists(NpcMemoryEntry entry)
    {
        if (entry == null) return;
        if (entry.recentTurns == null) entry.recentTurns = new List<string>();
        if (entry.memoryFacts == null) entry.memoryFacts = new List<string>();
    }

    private static void TrimList(List<string> list, int maxCount)
    {
        if (list == null || maxCount < 1) return;
        while (list.Count > maxCount)
            list.RemoveAt(0);
    }

    private static string GetSafeNpcId(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
            return "npc.unknown";

        return npcId.Trim();
    }

    private static string GetSafeSaveSlotId(string saveSlotId)
    {
        string slot = string.IsNullOrWhiteSpace(saveSlotId) ? k_DefaultSaveSlotId : saveSlotId.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
            slot = slot.Replace(invalid[i], '_');

        return string.IsNullOrWhiteSpace(slot) ? k_DefaultSaveSlotId : slot;
    }

    private static string GetFilePath(string saveSlotId)
    {
        string safeSlotId = GetSafeSaveSlotId(saveSlotId);
        return Path.Combine(Application.persistentDataPath, k_FilePrefix + safeSlotId + ".json");
    }
}
