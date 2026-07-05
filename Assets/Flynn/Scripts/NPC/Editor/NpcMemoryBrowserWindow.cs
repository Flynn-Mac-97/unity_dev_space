using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Flynn.Npc.Memory;


using Flynn.Core;
using Flynn.Npc;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc.Editor
{
    // Read/manage view over a save slot's unified LiteDB. Snapshots the DB on
    // refresh (open Shared → read all → close) so the window never holds a lock
    // while a Play session writes. Mutations open the DB briefly, apply, re-snapshot.
    public class NpcMemoryBrowserWindow : EditorWindow
    {
        private enum Tab { Npcs, World, Knowledge, Chat }

        private string[] _slots = new string[0];
        private int _slotIndex;
        private string _search = string.Empty;
        private Tab _tab = Tab.Npcs;
        private string _selectedNpc;
        private Vector2 _leftScroll, _rightScroll;

        // Snapshot
        private List<MemoryDoc> _memories = new List<MemoryDoc>();
        private List<KnowledgeDoc> _knowledge = new List<KnowledgeDoc>();
        private List<ChatTurnDoc> _chat = new List<ChatTurnDoc>();
        private List<ThingDoc> _things = new List<ThingDoc>();
        private List<NpcDoc> _npcs = new List<NpcDoc>();
        private List<SignalDoc> _signals = new List<SignalDoc>();
        private CommunityDoc _community;
        private List<string> _npcIds = new List<string>();

        [MenuItem("Flynn/NPC/Memory Browser")]
        public static void Open()
        {
            var w = GetWindow<NpcMemoryBrowserWindow>("NPC Memory");
            w.minSize = new Vector2(820f, 520f);
            w.RefreshSlots();
            w.Refresh();
            w.Show();
        }

        private void OnFocus() { RefreshSlots(); }

        private string CurrentSlot => (_slots.Length > 0 && _slotIndex >= 0 && _slotIndex < _slots.Length) ? _slots[_slotIndex] : null;

        private void RefreshSlots()
        {
            _slots = NpcMemoryDatabase.EnumerateSlots().ToArray();
            if (_slotIndex >= _slots.Length) _slotIndex = 0;
        }

        // ── Data access ─────────────────────────────────────────────────────────────

        private void Refresh()
        {
            _memories.Clear(); _knowledge.Clear(); _chat.Clear();
            _things.Clear(); _npcs.Clear(); _signals.Clear(); _community = null; _npcIds.Clear();

            string slot = CurrentSlot;
            if (string.IsNullOrEmpty(slot)) return;

            var db = new NpcMemoryDatabase();
            try
            {
                if (!db.OpenExisting(slot)) return;
                _memories = db.Memories.FindAll().ToList();
                _knowledge = db.Knowledge.FindAll().ToList();
                _chat = db.ChatTurns.FindAll().ToList();
                _things = db.Things.FindAll().ToList();
                _npcs = db.Npcs.FindAll().ToList();
                _signals = db.Signals.FindAll().ToList();
                _community = db.Community.FindAll().FirstOrDefault();

                var ids = new HashSet<string>();
                foreach (var n in _npcs) if (!string.IsNullOrEmpty(n.NpcId)) ids.Add(n.NpcId);
                foreach (var m in _memories) if (!string.IsNullOrEmpty(m.NpcId)) ids.Add(m.NpcId);
                foreach (var c in _chat) if (!string.IsNullOrEmpty(c.NpcId)) ids.Add(c.NpcId);
                _npcIds = ids.OrderBy(x => x).ToList();
            }
            catch (System.Exception e) { Debug.LogError("[NpcMemoryBrowser] " + e.Message); }
            finally { db.Dispose(); }

            if (string.IsNullOrEmpty(_selectedNpc) || !_npcIds.Contains(_selectedNpc))
                _selectedNpc = _npcIds.FirstOrDefault();
        }

        // Open the DB briefly, run a mutation, close, re-snapshot.
        private void Mutate(System.Action<NpcMemoryDatabase> op)
        {
            string slot = CurrentSlot;
            if (string.IsNullOrEmpty(slot)) return;
            var db = new NpcMemoryDatabase();
            try { if (db.OpenExisting(slot)) op(db); }
            catch (System.Exception e) { Debug.LogError("[NpcMemoryBrowser] mutate: " + e.Message); }
            finally { db.Dispose(); }
            Refresh();
        }

        // ── GUI ─────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawToolbar();
            if (_slots.Length == 0)
            {
                EditorGUILayout.HelpBox("No memory DB found. Play the scene with EmbeddingSettings assigned to create one.", MessageType.Info);
                return;
            }

            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "NPCs", "World", "Knowledge", "Chat" });
            EditorGUILayout.Space(4);

            switch (_tab)
            {
                case Tab.Npcs: DrawNpcsTab(); break;
                case Tab.World: DrawWorldTab(); break;
                case Tab.Knowledge: DrawKnowledgeTab(); break;
                case Tab.Chat: DrawChatTab(); break;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _slotIndex = EditorGUILayout.Popup(_slotIndex, _slots, EditorStyles.toolbarPopup, GUILayout.Width(160));
            if (EditorGUI.EndChangeCheck()) Refresh();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64))) Refresh();
            if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                string slot = CurrentSlot;
                if (!string.IsNullOrEmpty(slot)) EditorUtility.RevealInFinder(NpcMemoryDatabase.SlotDbPath(slot));
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Search", GUILayout.Width(46));
            _search = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.Width(220));

            if (GUILayout.Button("Clear All", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Clear All Memory",
                    "Delete ALL dynamic memories and chat in slot '" + CurrentSlot + "'? Authored content stays.", "Delete", "Cancel"))
                    Mutate(db => db.ClearAllMemory());
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool Match(string s) =>
            string.IsNullOrEmpty(_search) || (!string.IsNullOrEmpty(s) && s.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0);

        private static string Dot(float[] v) => (v != null && v.Length > 0) ? "●" : "○";

        // ── NPCs tab ────────────────────────────────────────────────────────────────

        private void DrawNpcsTab()
        {
            EditorGUILayout.BeginHorizontal();

            // Left: NPC list
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.Width(220));
            foreach (var id in _npcIds)
            {
                int memCount = _memories.Count(m => m.NpcId == id);
                bool sel = id == _selectedNpc;
                var style = sel ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button(id + "  (" + memCount + ")", style)) _selectedNpc = id;
            }
            EditorGUILayout.EndScrollView();

            // Right: selected NPC detail
            EditorGUILayout.BeginVertical();
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            if (!string.IsNullOrEmpty(_selectedNpc))
            {
                var npc = _npcs.FirstOrDefault(n => n.NpcId == _selectedNpc);
                EditorGUILayout.LabelField(npc != null ? npc.DisplayName + "  —  " + _selectedNpc : _selectedNpc, EditorStyles.boldLabel);
                if (npc != null)
                    EditorGUILayout.LabelField(npc.Role, EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button("Clear this NPC", GUILayout.Width(120)))
                    if (EditorUtility.DisplayDialog("Clear NPC", "Delete all memories + chat for '" + _selectedNpc + "'?", "Delete", "Cancel"))
                        Mutate(db => db.ClearNpc(_selectedNpc));

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("MEMORIES", EditorStyles.boldLabel);
                foreach (var m in _memories.Where(m => m.NpcId == _selectedNpc).Where(m => Match(m.Text)).OrderByDescending(m => m.CreatedUtc))
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"{Dot(m.Embedding)} [{m.Subject}] {m.Text}", EditorStyles.wordWrappedLabel);
                    GUILayout.Label($"imp {m.Importance:0.0} · ×{m.RecallCount}", GUILayout.Width(90));
                    if (GUILayout.Button("re-embed", GUILayout.Width(70)))
                        Mutate(db => { var d = db.Memories.FindById(m.Id); if (d != null) { d.Embedding = null; db.Memories.Update(d); } });
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                        Mutate(db => db.Memories.Delete(m.Id));
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        // ── World tab ───────────────────────────────────────────────────────────────

        private void DrawWorldTab()
        {
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_community != null)
            {
                EditorGUILayout.LabelField("COMMUNITY", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{Dot(_community.Embedding)} {_community.DisplayName} ({_community.CommunityId})", EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(_community.Overview)) EditorGUILayout.LabelField(_community.Overview, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.LabelField("THINGS (" + _things.Count + ")", EditorStyles.boldLabel);
            foreach (var t in _things.Where(t => Match(t.DisplayName) || Match(t.ShortDescription) || Match(t.ThingId)))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{Dot(t.Embedding)} {t.DisplayName}  ({t.ThingId})", EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(t.ShortDescription)) EditorGUILayout.LabelField(t.ShortDescription, EditorStyles.wordWrappedMiniLabel);
                // Referenced-by: NPCs who know it, signals that fire on it.
                var knowers = _knowledge.Where(k => k.ThingId == t.ThingId).Select(k => k.OwnerScope).Distinct().ToList();
                var sigs = _signals.Where(s => s.ThingId == t.ThingId).Select(s => s.SignalId).ToList();
                if (knowers.Count > 0) EditorGUILayout.LabelField("known by: " + string.Join(", ", knowers), EditorStyles.miniLabel);
                if (sigs.Count > 0) EditorGUILayout.LabelField("signals: " + string.Join(", ", sigs), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("SIGNALS (" + _signals.Count + ")", EditorStyles.boldLabel);
            foreach (var s in _signals.Where(s => Match(s.SignalId) || Match(s.Description)))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{s.SignalId}  →  {(string.IsNullOrEmpty(s.ThingId) ? "(untied)" : s.ThingId)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{s.Description}   [trust≥{s.MinTrustToFire}{(s.Repeatable ? ", repeatable" : "")}]", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Knowledge tab ───────────────────────────────────────────────────────────

        private void DrawKnowledgeTab()
        {
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            EditorGUILayout.LabelField("AUTHORED KNOWLEDGE (" + _knowledge.Count + ")", EditorStyles.boldLabel);
            foreach (var k in _knowledge.Where(k => Match(k.Text) || Match(k.OwnerScope) || Match(k.ThingId)))
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                string thing = string.IsNullOrEmpty(k.ThingId) ? "" : " →" + k.ThingId;
                EditorGUILayout.LabelField($"{Dot(k.Embedding)} [{k.OwnerScope}{thing}] ({k.Kind}) {k.Text}", EditorStyles.wordWrappedLabel);
                if (k.RevealTrust > 0) GUILayout.Label("≥" + k.RevealTrust, GUILayout.Width(40));
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                    Mutate(db => db.Knowledge.Delete(k.Id));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        // ── Chat tab ────────────────────────────────────────────────────────────────

        private void DrawChatTab()
        {
            EditorGUILayout.BeginHorizontal();
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.Width(220));
            foreach (var id in _npcIds)
            {
                int c = _chat.Count(t => t.NpcId == id);
                if (GUILayout.Button(id + "  (" + c + ")", id == _selectedNpc ? EditorStyles.boldLabel : EditorStyles.label))
                    _selectedNpc = id;
            }
            EditorGUILayout.EndScrollView();

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            foreach (var t in _chat.Where(t => t.NpcId == _selectedNpc).Where(t => Match(t.Content)).OrderBy(t => t.TurnIndex))
                EditorGUILayout.LabelField($"{t.TurnIndex,3}  {t.Speaker}: {t.Content}", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
        }
    }

}
