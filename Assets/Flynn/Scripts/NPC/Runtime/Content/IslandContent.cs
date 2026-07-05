using System;
using System.Collections.Generic;
using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Npc
{
    // Plain-old serializable mirrors of the island JSON authored under
    // Assets/Flynn/Configs/NPC/Islands/<island>.json. JsonUtility-friendly:
    // public fields, no dictionaries, no nullables, no top-level arrays.
    //
    // Enums are deliberately avoided — kind/handler are free strings so the
    // authored JSON stays human-readable and the C# side never has to grow a
    // case for every new authored value. The allowed values live in
    // IslandContentVocab and are enforced by IslandContentValidator, not the type
    // system, so bad values surface as validation issues rather than silent drops.

    [Serializable]
    public class IslandContent
    {
        public string islandId;
        public CommunityContent community;
        public List<WorldThingContent> things = new List<WorldThingContent>();
        public List<SignalContent> signals = new List<SignalContent>();
        public List<NpcContent> npcs = new List<NpcContent>();
        public DesignerNotes designerNotes;
    }

    [Serializable]
    public class CommunityContent
    {
        public string communityId;
        public string displayName;
        public string overview;
        public string currentSituation;
        public string sharedMood;
        public string culture;
        public List<KnowledgeContent> knowledge = new List<KnowledgeContent>();
    }

    [Serializable]
    public class WorldThingContent
    {
        public string thingId;
        public string displayName;
        public List<string> aliases = new List<string>();
        public List<string> tags = new List<string>();
        public string shortDescription;
    }

    [Serializable]
    public class SignalContent
    {
        public string signalId;
        // Empty string means "not tied to a specific thing" (e.g. first_meeting).
        public string thingId;
        public string description;
        // Player-facing title shown on objective chips and codex Tasks tab.
        // Falls back to description if empty.
        public string title;
        public bool repeatable;
        public int minTrustToFire;
        // Categorisation from IslandContentVocab.SignalHandlers. Delivered to
        // scene listeners via DialogueTriggerPayload.handler so reactions can
        // route on it if they want; matching is still primarily by signalId.
        public string handler;
        // Free-form payload string the handler interprets. Delivered verbatim to
        // listeners via DialogueTriggerPayload.payloadJson. Usually JSON, but the
        // runtime treats it as opaque text.
        public string payloadJson;
    }

    [Serializable]
    public class NpcContent
    {
        public string npcId;
        public string displayName;
        public string role;
        public string speakingStyle;
        public List<string> personalityTraits = new List<string>();
        public List<string> doRules = new List<string>();
        public List<string> dontRules = new List<string>();
        public List<string> capabilities = new List<string>();
        public int startingTrust;
        public int trustToShareSecrets;
        public string fallbackReply;
        public List<KnowledgeContent> knowledge = new List<KnowledgeContent>();
        public NpcDesignerNotes designerNotes;
    }

    [Serializable]
    public class KnowledgeContent
    {
        public string id;
        public string thingId;
        // One of IslandContentVocab.KnowledgeKinds.
        public string kind;
        public string text;
        public int revealTrustThreshold;
    }

    // Per-NPC staging notes for designers. Kept on the NPC so the visual story
    // and the dialogue story live next to each other.
    [Serializable]
    public class NpcDesignerNotes
    {
        public string placement;
        public List<string> setDressing = new List<string>();
        public string visualStory;
        public string playerRead;
    }

    [Serializable]
    public class DesignerNotes
    {
        public List<string> environmentSetDressing = new List<string>();
        public List<string> visualStorytelling = new List<string>();
        public List<SuggestedInteractable> suggestedInteractables = new List<SuggestedInteractable>();
        public List<string> lightingAndMood = new List<string>();
        public List<string> implementationChecklist = new List<string>();
    }

    [Serializable]
    public class SuggestedInteractable
    {
        public string thingId;
        public string instruction;
    }

}
