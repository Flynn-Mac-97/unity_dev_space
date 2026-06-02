using System;
using UnityEngine;

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

{""reply"":""1-3 short in-character sentences; the ONLY field the player sees"",""topic"":""short noun phrase"",""intent"":""short verb phrase"",""tone"":""one adjective"",""mood_shift"":""calmer|tenser|same"",""relationship_deltas"":{""trust"":0,""affection"":0,""suspicion"":0},""flags"":[],""suggested_player_replies"":[""opt1"",""opt2"",""opt3""],""memory_updates"":[{""subject"":""player|self|world|relationship|disclosure"",""fact"":""one new fact from this turn""}],""triggers_fired"":[]}

Rules:
- relationship_deltas: integers -2..+2, default 0; move only when this turn earns it.
- flags: lower_snake_case tags, [] if none.
- suggested_player_replies: exactly three, distinct stances, under 12 words each, in the player's voice.
- memory_updates: 0-3 items; pure small talk = []. Each fact is ONE present-tense sentence under 140 chars that names its subject (e.g. ""Maren told Flynn...""), is NEW (not already in Known facts or your persona), and is not an echo of your reply. Skip near-duplicates and vague mood restatements. subject is who/what it's about:
    player = who the player is or what they did; self = what YOU decided/committed to; world = a named place/person/item/event/rule they just learned; relationship = how you two now regard each other (max one); disclosure = something you just told them, so you don't repeat it.
- triggers_fired: default []. Only ids from the signals list above, only when your reply genuinely does that thing; never invent ids; usually at most one.

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
