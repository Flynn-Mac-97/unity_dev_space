using System;
using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc
{
    // Structured envelope the NPC model is asked to return. JsonUtility-friendly:
    // all fields public, only primitives + nested [Serializable] + string[].
    [Serializable]
    public class NpcReplyEnvelope
    {
        public string reply;
        public string topic;
        public string intent;
        public string tone;
        public string mood_shift;
        public RelationshipDeltas relationship_deltas;
        public string[] flags;
        public string[] suggested_player_replies;
        public string[] suggested_reply_verbs;   // parallel to suggested_player_replies: ask|press|joke|show
        public MemoryUpdate[] memory_updates;
        public string[] triggers_fired;

        [Serializable]
        public class RelationshipDeltas
        {
            public int trust;
            public int affection;
            public int suspicion;
        }

        [Serializable]
        public class MemoryUpdate
        {
            public string subject; // "player" | "self" | "world"
            public string fact;
        }

        // The prompt block appended to every NPC system prompt. Keep this and the
        // envelope schema above in lockstep — model output and parser must agree.
        // Kept deliberately tight: one compact schema + inline field rules, no
        // worked-example pedagogy, so small local models follow it without drowning.
        public const string PromptAddendum =
    @"Reply with ONE JSON object only — no prose, no markdown fences. Include every key (use [] or null when empty):

    {""reply"":""1-3 short in-character sentences; the ONLY field the player sees"",""topic"":""short noun phrase"",""intent"":""short verb phrase"",""tone"":""one adjective"",""mood_shift"":""calmer|tenser|same"",""relationship_deltas"":{""trust"":0,""affection"":0,""suspicion"":0},""flags"":[],""suggested_player_replies"":[""opt1"",""opt2"",""opt3""],""suggested_reply_verbs"":[""ask"",""press"",""joke""],""memory_updates"":[{""subject"":""player|self|world|relationship|disclosure"",""fact"":""one new fact from this turn""}],""triggers_fired"":[]}

    Rules:
    - relationship_deltas: integers -2..+2. Award +1 trust when the player is genuinely helpful, asks thoughtful questions, or shows care. Award +2 for significant moments (offers to help, promises action). Use -1 only if the player is rude or dismissive. Default 0 only for pure filler.
    - flags: lower_snake_case tags, [] if none.
    - suggested_player_replies: exactly three, distinct stances, under 12 words each, in the player's voice.
    - suggested_reply_verbs: one verb per suggested reply, same order. ask = neutral question or statement. press = pushing, confronting, demanding — risky for the player: when a press touches a topic you avoid, answer guarded and add suspicion +1 (or +2 if pressed repeatedly), but at high trust you may yield more than you normally would. joke = playful or teasing — when it lands well add affection +1. Offer at most ONE press option, and only when there is genuinely something to push on.
    - Items: a player line in the form ""(I hold out my X to show you.)"" means they are SHOWING you an object — react to it in character, comment on what it means to you. A line in the form ""(I hand you my X — a gift.)"" means they are GIVING it to you: if it is something you value or find meaningful, add trust +1 and affection +1 (or +2 for something deeply meaningful), thank them in your own voice, and record a memory_update about the gift (subject: relationship). If the gift is worthless to you, be polite but honest; still add a memory_update.
    - memory_updates: record 1-2 on MOST turns; use [] only for pure greetings or filler. Capture anything the player revealed about themselves or decided, any named place/person/item/event/rule that came up, or something notable you just told them (subject: disclosure). Each is ONE present-tense sentence under 140 chars that names its subject (e.g. ""Sela warned Flynn the cave is only safe when the wind is steady.""), is NEW (not already in Known facts or your persona), and is not a word-for-word echo of your reply. subject is who/what it's about:
        player = who the player is or what they did; self = what YOU decided/committed to; world = a named place/person/item/event/rule they just learned; relationship = how you two now regard each other (max one); disclosure = something you just told them, so you don't repeat it.
    - triggers_fired: default []. Only ids from the signals list above. You MUST fire a signal when your reply matches its description — do NOT be conservative. Fire first_meeting signals on the first real exchange. Fire objective/tutorial signals the moment you instruct or ask the player to do that task. If your reply tells the player to do something and a signal description matches that action, fire it. Never invent ids; usually at most one.

    Output valid JSON. No trailing commas. No text outside the object.";

        /// Try to parse the model's raw output. Tolerates ```json fences and stray prose
        /// by extracting the first {...} block. Returns null if no usable envelope found.
        public static NpcReplyEnvelope TryParse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string json = ExtractJsonObject(raw);
            if (json == null) return null;
            try
            {
                var parsed = JsonUtility.FromJson<NpcReplyEnvelope>(json);
                return parsed;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NpcReplyEnvelope] Parse failed: " + e.Message + " body=" + json);
                return null;
            }
        }

        private static string ExtractJsonObject(string raw)
        {
            string s = raw.Trim();
            // Strip ```json ... ``` or ``` ... ``` fences if present.
            if (s.StartsWith("```"))
            {
                int firstNewline = s.IndexOf('\n');
                if (firstNewline > 0) s = s.Substring(firstNewline + 1);
                int closingFence = s.LastIndexOf("```", StringComparison.Ordinal);
                if (closingFence >= 0) s = s.Substring(0, closingFence);
                s = s.Trim();
            }

            // Find the outermost {...} block. Counts braces, ignores braces inside strings.
            int start = s.IndexOf('{');
            if (start < 0) return null;
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return s.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        public string ToDebugString()
        {
            // Single-line on purpose — Unity's console list truncates at the first
            // newline, so a pretty-printed JSON shows up as just "{" in the dock.
            try { return JsonUtility.ToJson(this, false); }
            catch { return "(serialize failed)"; }
        }

        public static string FlattenForLog(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }

}
