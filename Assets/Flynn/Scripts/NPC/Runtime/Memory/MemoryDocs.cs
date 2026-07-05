using System;

using Flynn.Core;
using Flynn.Npc;
using Flynn.UI.Core;

// LiteDB document POCOs for the NPC memory database. Plain classes with public
// auto-properties so LiteDB's BsonMapper serializes them without attributes.
// `Id` is the auto-increment primary key for each collection.
//
// Vectors live on the document as a float[] (stored as a BSON array). Recall is
// a brute-force cosine over a Find-filtered candidate set — see NpcMemoryDatabase.
namespace Flynn.Npc.Memory
{
    // Collection name constants kept in one place so the DB wrapper, migration
    // tool, and any future doc types agree on the strings.
    public static class MemoryCollections
    {
        // Dynamic (player-created, preserved across re-imports)
        public const string Memories  = "memories";
        public const string ChatTurns = "chat_turns";
        public const string Meta      = "meta";

        // Authored (imported from island JSON, reseeded per islandId)
        public const string Community = "community";
        public const string Things    = "things";
        public const string Npcs      = "npcs";
        public const string Signals   = "signals";
        public const string Knowledge = "knowledge";
        public const string FiredSignals = "fired_signals";

        // Player-facing codex (facts the player has learned from NPCs or scans)
        public const string PlayerCodex = "player_codex";
    }

    public static class MemorySource
    {
        public const string Dialogue = "dialogue";
        public const string Authored = "authored";
        public const string Gossip   = "gossip";
    }

    // A dynamic fact an NPC has accreted about the player / itself / the world.
    public class MemoryDoc
    {
        public int Id { get; set; }
        public string NpcId { get; set; }
        // player | self | world | relationship | disclosure
        public string Subject { get; set; }
        public string Text { get; set; }
        public float Importance { get; set; } = 1f;
        public int CreatedTurn { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastRecalledUtc { get; set; }
        public int RecallCount { get; set; }
        // dialogue | authored | gossip
        public string Source { get; set; } = MemorySource.Dialogue;
        public float[] Embedding { get; set; }
    }

    // Authored island knowledge mirrored into the DB so it participates in the
    // same semantic recall as dynamic memory. OwnerScope is an npcId or
    // "community". Re-derived from island JSON on load; not player-mutated.
    public class KnowledgeDoc
    {
        public int Id { get; set; }
        public string IslandId { get; set; }
        // Authored knowledge entry id from the island JSON (for round-trip/export).
        public string KnowledgeId { get; set; }
        public string OwnerScope { get; set; } // npcId | "community"
        public string ThingId { get; set; }
        public string Kind { get; set; }
        public string Text { get; set; }
        public int RevealTrust { get; set; }
        // Stable hash of (IslandId|OwnerScope|ThingId|Text) used to skip
        // re-embedding unchanged authored knowledge across loads.
        public string ContentHash { get; set; }
        public float[] Embedding { get; set; }
    }

    // One line of conversation, persisted so chat history survives quit. The
    // live prompt window is still capped by LlmPromptConfig.recentTurnsLimit.
    public class ChatTurnDoc
    {
        public int Id { get; set; }
        public string NpcId { get; set; }
        public string Speaker { get; set; }
        public string Content { get; set; }
        public int TurnIndex { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    // Single-row bookkeeping for schema/embedding compatibility checks.
    public class MetaDoc
    {
        public int Id { get; set; }
        public int SchemaVersion { get; set; }
        public string EmbeddingModel { get; set; }
        public int Dim { get; set; }
    }

    // ── Authored entities (mirrored from island JSON) ──────────────────────────
    // Each carries IslandId (for reseed-by-island) and ContentHash (idempotent
    // re-import: unchanged rows are skipped, changed rows re-embedded).

    public class CommunityDoc
    {
        public int Id { get; set; }
        public string IslandId { get; set; }
        public string CommunityId { get; set; }
        public string DisplayName { get; set; }
        public string Overview { get; set; }
        public string CurrentSituation { get; set; }
        public string SharedMood { get; set; }
        public string Culture { get; set; }
        public string ContentHash { get; set; }
        public float[] Embedding { get; set; }
    }

    public class ThingDoc
    {
        public int Id { get; set; }
        public string IslandId { get; set; }
        public string ThingId { get; set; }
        public string DisplayName { get; set; }
        public System.Collections.Generic.List<string> Aliases { get; set; }
        public System.Collections.Generic.List<string> Tags { get; set; }
        public string ShortDescription { get; set; }
        public string ContentHash { get; set; }
        public float[] Embedding { get; set; }
    }

    public class NpcDoc
    {
        public int Id { get; set; }
        public string IslandId { get; set; }
        public string NpcId { get; set; }
        public string DisplayName { get; set; }
        public string Role { get; set; }
        public string SpeakingStyle { get; set; }
        public System.Collections.Generic.List<string> PersonalityTraits { get; set; }
        public System.Collections.Generic.List<string> DoRules { get; set; }
        public System.Collections.Generic.List<string> DontRules { get; set; }
        public System.Collections.Generic.List<string> Capabilities { get; set; }
        public int StartingTrust { get; set; }
        public int TrustToShareSecrets { get; set; }
        public string FallbackReply { get; set; }
        // DesignerNotes kept as raw JSON so the doc stays flat; rehydrated on export.
        public string DesignerNotesJson { get; set; }
        public string ContentHash { get; set; }
        public float[] Embedding { get; set; }
    }

    public class SignalDoc
    {
        public int Id { get; set; }
        public string IslandId { get; set; }
        public string SignalId { get; set; }
        public string ThingId { get; set; }
        public string Description { get; set; }
        public bool Repeatable { get; set; }
        public int MinTrustToFire { get; set; }
        public string Handler { get; set; }
        public string PayloadJson { get; set; }
        public string ContentHash { get; set; }
    }

    // Tracks which non-repeatable signals an NPC has already fired, so the
    // model can't replay them. Keyed by NpcId + SignalId.
    public class FiredSignalDoc
    {
        public int Id { get; set; }
        public string NpcId { get; set; }
        public string SignalId { get; set; }
        public DateTime FiredUtc { get; set; }
    }
}
