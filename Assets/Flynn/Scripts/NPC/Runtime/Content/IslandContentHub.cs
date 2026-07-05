using System;
using System.Collections.Generic;
using UnityEngine;
using Flynn.Npc.Memory;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Npc
{
    // Loads one island JSON (authored under Assets/Flynn/Configs/NPC/Islands/)
    // and exposes simple id-keyed lookups. No SO, no singleton — drop the
    // component on a scene root and reference it from SceneLlmManager (or
    // query it directly) when the resolver lands.
    //
    // Lookups are rebuilt on Awake and on any LoadFrom call so designers can
    // hot-swap content in the editor without restarting Play.
    public class IslandContentHub : MonoBehaviour
    {
        [Tooltip("Authored island content. Drop the .json TextAsset (e.g. windroot_hamlet.json) here.")]
        public TextAsset islandJson;

        private IslandContent _content;
        private readonly Dictionary<string, NpcContent> _npcById = new Dictionary<string, NpcContent>(StringComparer.Ordinal);
        private readonly Dictionary<string, WorldThingContent> _thingById = new Dictionary<string, WorldThingContent>(StringComparer.Ordinal);
        private readonly Dictionary<string, WorldThingContent> _thingByAlias = new Dictionary<string, WorldThingContent>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SignalContent> _signalById = new Dictionary<string, SignalContent>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<SignalContent>> _signalsByThing = new Dictionary<string, List<SignalContent>>(StringComparer.Ordinal);

        public IslandContent Content => _content;
        public bool IsLoaded => _content != null;

        private void Awake()
        {
            if (islandJson != null) LoadFrom(islandJson);
        }

        public bool LoadFrom(TextAsset asset)
        {
            if (asset == null) { Clear(); return false; }
            return LoadFromJson(asset.text, asset.name);
        }

        public bool LoadFromJson(string json, string sourceLabel = "(inline)")
        {
            Clear();
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[IslandContentHub] Empty island JSON: " + sourceLabel);
                return false;
            }
            try { _content = JsonUtility.FromJson<IslandContent>(json); }
            catch (Exception e)
            {
                Debug.LogError("[IslandContentHub] Failed to parse island JSON '" + sourceLabel + "': " + e.Message);
                _content = null;
                return false;
            }
            if (_content == null)
            {
                Debug.LogError("[IslandContentHub] JsonUtility returned null for '" + sourceLabel + "'.");
                return false;
            }
            BuildIndices();
            return true;
        }

        // Rebuilds the runtime content from the unified LiteDB instead of JSON. JSON
        // remains the canonical seed (the importer mirrors it into the DB); this makes
        // the DB the runtime source so authored edits + future DB-only content flow
        // through the same resolver/prompt path. POCO shapes are unchanged, so no
        // downstream consumer is affected.
        public bool LoadFromDatabase(NpcMemoryDatabase db, string islandId)
        {
            var content = IslandContentDb.ToContent(db, islandId);
            if (content == null) return false;
            Clear();
            _content = content;
            BuildIndices();
            Debug.Log($"[IslandContentHub] Loaded '{islandId}' from DB: {content.npcs.Count} npcs, {content.things.Count} things, {content.signals.Count} signals.");
            return true;
        }

        private void BuildIndices()
        {
            if (_content.npcs != null)
            {
                foreach (var n in _content.npcs)
                {
                    if (n == null || string.IsNullOrEmpty(n.npcId)) continue;
                    _npcById[n.npcId] = n;
                }
            }
            if (_content.things != null)
            {
                foreach (var t in _content.things)
                {
                    if (t == null || string.IsNullOrEmpty(t.thingId)) continue;
                    _thingById[t.thingId] = t;
                    if (!string.IsNullOrWhiteSpace(t.displayName))
                        _thingByAlias[t.displayName.Trim()] = t;
                    if (t.aliases != null)
                    {
                        foreach (var alias in t.aliases)
                        {
                            if (string.IsNullOrWhiteSpace(alias)) continue;
                            _thingByAlias[alias.Trim()] = t;
                        }
                    }
                }
            }
            if (_content.signals != null)
            {
                foreach (var s in _content.signals)
                {
                    if (s == null || string.IsNullOrEmpty(s.signalId)) continue;
                    _signalById[s.signalId] = s;
                    string key = s.thingId ?? string.Empty;
                    if (!_signalsByThing.TryGetValue(key, out var list))
                    {
                        list = new List<SignalContent>();
                        _signalsByThing[key] = list;
                    }
                    list.Add(s);
                }
            }
        }

        private void Clear()
        {
            _content = null;
            _npcById.Clear();
            _thingById.Clear();
            _thingByAlias.Clear();
            _signalById.Clear();
            _signalsByThing.Clear();
        }

        // ── Queries ──────────────────────────────────────────────────────────────

        public NpcContent GetNpc(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return null;
            _npcById.TryGetValue(npcId, out var npc);
            return npc;
        }

        public WorldThingContent GetThing(string thingId)
        {
            if (string.IsNullOrEmpty(thingId)) return null;
            _thingById.TryGetValue(thingId, out var thing);
            return thing;
        }

        public CommunityContent GetCommunity() => _content != null ? _content.community : null;

        public SignalContent GetSignal(string signalId)
        {
            if (string.IsNullOrEmpty(signalId)) return null;
            _signalById.TryGetValue(signalId, out var s);
            return s;
        }

        // Signals tied to a given thing. Pass empty string to fetch the
        // "no-thing" bucket (things like first-meeting beats).
        public IReadOnlyList<SignalContent> GetSignalsForThing(string thingId)
        {
            string key = thingId ?? string.Empty;
            return _signalsByThing.TryGetValue(key, out var list)
                ? (IReadOnlyList<SignalContent>)list
                : Array.Empty<SignalContent>();
        }

        // Best-effort alias resolution. Tries exact-case thingId first, then
        // case-insensitive alias/displayName match. Returns null if nothing
        // looks plausible. Substring scans live in the resolver, not here.
        public WorldThingContent ResolveByAlias(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string trimmed = text.Trim();
            if (_thingById.TryGetValue(trimmed, out var direct)) return direct;
            return _thingByAlias.TryGetValue(trimmed, out var byAlias) ? byAlias : null;
        }

        public IEnumerable<KnowledgeContent> GetNpcKnowledgeForThing(string npcId, string thingId, int trust)
        {
            var npc = GetNpc(npcId);
            if (npc == null || npc.knowledge == null) yield break;
            foreach (var k in npc.knowledge)
            {
                if (k == null || string.IsNullOrWhiteSpace(k.text)) continue;
                if (!string.IsNullOrEmpty(thingId) && !string.Equals(k.thingId, thingId, StringComparison.Ordinal)) continue;
                if (k.revealTrustThreshold > 0 && trust < k.revealTrustThreshold) continue;
                yield return k;
            }
        }

        public IEnumerable<KnowledgeContent> GetCommunityKnowledgeForThing(string thingId, int trust)
        {
            var community = GetCommunity();
            if (community == null || community.knowledge == null) yield break;
            foreach (var k in community.knowledge)
            {
                if (k == null || string.IsNullOrWhiteSpace(k.text)) continue;
                if (!string.IsNullOrEmpty(thingId) && !string.Equals(k.thingId, thingId, StringComparison.Ordinal)) continue;
                if (k.revealTrustThreshold > 0 && trust < k.revealTrustThreshold) continue;
                yield return k;
            }
        }
    }

}
