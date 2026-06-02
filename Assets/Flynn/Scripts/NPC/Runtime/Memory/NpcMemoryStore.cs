using System;
using System.Collections.Generic;
using UnityEngine;

// Inspectable save slot for NPC dialogue memory. One asset = one slot; every
// NPC that has ever spoken gets an Entry inside this asset's `entries` list.
// In the Editor, select the asset to see exactly what each NPC remembers
// while the conversation is live.
//
// Persistence model:
//   - Editor:   SetDirty per mutation means the inspector refreshes live.
//               Explicit Save() flushes to disk and survives Play → Edit and
//               editor restarts.
//   - Player builds: SO mutations are runtime-only and lost on quit. If you
//               need build-side persistence, add a JSON sidecar later.
[CreateAssetMenu(menuName = "Dialogue/NPC Memory Store", fileName = "NpcMemoryStore")]
public class NpcMemoryStore : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string npcId;
        public List<string> recentTurns = new List<string>();
        public List<string> memoryFacts = new List<string>();
        public List<string> firedTriggers = new List<string>();
    }

    [Tooltip("One Entry per NPC who has ever spoken to the player in this slot. Live-editable in the inspector while a conversation runs.")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    public Entry GetOrCreate(string npcId)
    {
        string safe = SafeNpcId(npcId);
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].npcId, safe, StringComparison.OrdinalIgnoreCase))
            {
                EnsureLists(entries[i]);
                return entries[i];
            }
        }
        var created = new Entry { npcId = safe };
        entries.Add(created);
        MarkDirty();
        return created;
    }

    public void AddTurn(string npcId, string speaker, string content, int recentTurnsLimit)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var entry = GetOrCreate(npcId);
        string safeSpeaker = string.IsNullOrWhiteSpace(speaker) ? "Unknown" : speaker.Trim();
        entry.recentTurns.Add(string.Format("{0}: {1}", safeSpeaker, content.Trim()));
        TrimList(entry.recentTurns, Mathf.Max(2, recentTurnsLimit));
        MarkDirty();
    }

    public void AddFact(string npcId, string fact, int factsLimit, int maxFactLength)
    {
        if (string.IsNullOrWhiteSpace(fact)) return;
        var entry = GetOrCreate(npcId);
        string trimmed = fact.Trim();
        if (maxFactLength > 0 && trimmed.Length > maxFactLength)
            trimmed = trimmed.Substring(0, maxFactLength);
        if (string.IsNullOrWhiteSpace(trimmed)) return;

        for (int i = 0; i < entry.memoryFacts.Count; i++)
        {
            if (string.Equals(entry.memoryFacts[i], trimmed, StringComparison.OrdinalIgnoreCase))
                return; // exact duplicate
            if (IsNearDuplicate(entry.memoryFacts[i], trimmed))
                return; // near-duplicate (e.g. "feels hopeful about X" vs "feels hopeful about X in the village")
        }
        entry.memoryFacts.Add(trimmed);
        TrimList(entry.memoryFacts, Mathf.Max(1, factsLimit));
        MarkDirty();
    }

    public bool ClearNpc(string npcId)
    {
        string safe = SafeNpcId(npcId);
        bool removed = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (string.Equals(entries[i].npcId, safe, StringComparison.OrdinalIgnoreCase))
            {
                entries.RemoveAt(i);
                removed = true;
            }
        }
        if (removed) { MarkDirty(); Save(); }
        return removed;
    }

    public void ClearAll()
    {
        if (entries.Count == 0) return;
        entries.Clear();
        MarkDirty();
        Save();
    }

    public bool WasTriggerFired(string npcId, string triggerKey)
    {
        if (string.IsNullOrWhiteSpace(triggerKey)) return false;
        var entry = GetOrCreate(npcId);
        for (int i = 0; i < entry.firedTriggers.Count; i++)
            if (string.Equals(entry.firedTriggers[i], triggerKey, StringComparison.Ordinal)) return true;
        return false;
    }

    public void MarkTriggerFired(string npcId, string triggerKey)
    {
        if (string.IsNullOrWhiteSpace(triggerKey)) return;
        var entry = GetOrCreate(npcId);
        for (int i = 0; i < entry.firedTriggers.Count; i++)
            if (string.Equals(entry.firedTriggers[i], triggerKey, StringComparison.Ordinal)) return;
        entry.firedTriggers.Add(triggerKey);
        MarkDirty();
    }

    // Explicit flush. Called on the /save chat command and on Clear ops.
    public void Save()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // Word-overlap (Jaccard) near-duplicate test. Catches the common failure of
    // the model re-asserting the same fact with a few extra words each turn.
    private static bool IsNearDuplicate(string a, string b)
    {
        var sa = TokenizeFact(a);
        var sb = TokenizeFact(b);
        if (sa.Count == 0 || sb.Count == 0) return false;
        int inter = 0;
        foreach (var t in sa) if (sb.Contains(t)) inter++;
        int union = sa.Count + sb.Count - inter;
        return union > 0 && (float)inter / union >= 0.6f;
    }

    private static readonly char[] k_FactSeparators = { ' ', '.', ',', ';', ':', '[', ']', '—', '-', '\'', '"', '!', '?' };

    private static HashSet<string> TokenizeFact(string s)
    {
        var set = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(s)) return set;
        foreach (var w in s.ToLowerInvariant().Split(k_FactSeparators, StringSplitOptions.RemoveEmptyEntries))
            if (w.Length >= 3) set.Add(w);
        return set;
    }

    private static void EnsureLists(Entry e)
    {
        if (e.recentTurns   == null) e.recentTurns   = new List<string>();
        if (e.memoryFacts   == null) e.memoryFacts   = new List<string>();
        if (e.firedTriggers == null) e.firedTriggers = new List<string>();
    }

    private static void TrimList(List<string> list, int maxCount)
    {
        if (list == null || maxCount < 1) return;
        while (list.Count > maxCount) list.RemoveAt(0);
    }

    private static string SafeNpcId(string npcId) =>
        string.IsNullOrWhiteSpace(npcId) ? "npc.unknown" : npcId.Trim();
}
