using System;

namespace Flynn.Npc.Memory
{
    // A piece of knowledge the player has learned — from NPC dialogue (disclosure)
    // or from scanning world objects. Lives in the same LiteDB as NPC memory so
    // persistence works without a second DB file.
    //
    // State transitions:
    //   Revealed  — normal, the player knows this fact
    //   Encrypted — from an untranslated scan; FactText holds the real text but
    //               EncryptedText is what the player sees until an NPC translates it
    public class PlayerCodexEntry
    {
        public int Id { get; set; }

        // Who or what told the player. "npc.transmitter_station" for dialogue,
        // "scan" for world scans.
        public string SourceNpcId { get; set; }

        // The envelope.topic from the dialogue turn when this was learned.
        public string Topic { get; set; }

        // Associated world thing (thingId from island JSON), if any.
        public string ThingId { get; set; }

        // The actual knowledge text — what the NPC said or what the scan reveals.
        public string FactText { get; set; }

        // Mirrors KnowledgeContent.kind: "fact" | "belief" | "rumor" | "secret" | "disclosure"
        public string Kind { get; set; }

        // True when this came from an encrypted scan that hasn't been translated yet.
        public bool IsEncrypted { get; set; }

        // The jumbled/corrupted text shown to the player before translation.
        public string EncryptedText { get; set; }

        public DateTime LearnedUtc { get; set; }

        // True after the player has opened the codex and seen this entry.
        // Used for the NEW badge — entries are NEW until the codex is opened.
        public bool HasBeenViewed { get; set; }

        // Stable hash of FactText for dedup — avoids adding the same fact twice.
        public string ContentHash { get; set; }
    }
}
