using System;
using System.Text.RegularExpressions;
using UnityEngine;

public static class NpcLlmResponseParser
{
    public const int MaxDeltaPerTurn = 5;

    public struct ParsedTurn
    {
        public string topic;
        public int trustDelta;
        public int affectionDelta;
        public int suspicionDelta;
        public string reply;
        public bool hadStructuredTags;
    }

    public const string SchemaInstructions =
        "OUTPUT FORMAT (you must follow this exact format):\n" +
        "TRUST: <integer between -3 and +3 — how much this turn changed your trust>\n" +
        "AFFECTION: <integer between -3 and +3>\n" +
        "SUSPICION: <integer between -3 and +3>\n" +
        "TOPIC: <short topic label the player just brought up, or 'none'>\n" +
        "REPLY:\n" +
        "<your in-character response to the player, 1-4 lines, no tags inside this section>\n" +
        "\n" +
        "Example:\n" +
        "TRUST: +1\n" +
        "AFFECTION: 0\n" +
        "SUSPICION: 0\n" +
        "TOPIC: Jorin the Technician\n" +
        "REPLY:\n" +
        "Jorin? Aye, he lives south. Builds anything that turns or blows.";

    private static readonly Regex s_TrustRegex     = new Regex(@"^\s*TRUST\s*:\s*([+\-]?\d+)\s*$",     RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex s_AffectionRegex = new Regex(@"^\s*AFFECTION\s*:\s*([+\-]?\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex s_SuspicionRegex = new Regex(@"^\s*SUSPICION\s*:\s*([+\-]?\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex s_TopicRegex     = new Regex(@"^\s*TOPIC\s*:\s*(.+?)\s*$",          RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex s_ReplySplit     = new Regex(@"^\s*REPLY\s*:\s*\n?",                RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public static ParsedTurn Parse(string raw)
    {
        var result = new ParsedTurn { topic = "none", reply = raw ?? string.Empty };

        if (string.IsNullOrWhiteSpace(raw)) return result;

        var split = s_ReplySplit.Match(raw);
        string tagsBlock;
        string replyBlock;

        if (split.Success)
        {
            tagsBlock = raw.Substring(0, split.Index);
            replyBlock = raw.Substring(split.Index + split.Length);
            result.hadStructuredTags = true;
        }
        else
        {
            tagsBlock = raw;
            replyBlock = raw;
        }

        bool foundAnyTag = false;

        if (TryExtractInt(s_TrustRegex,     tagsBlock, out int trust))     { result.trustDelta     = ClampDelta(trust);     foundAnyTag = true; }
        if (TryExtractInt(s_AffectionRegex, tagsBlock, out int affection)) { result.affectionDelta = ClampDelta(affection); foundAnyTag = true; }
        if (TryExtractInt(s_SuspicionRegex, tagsBlock, out int suspicion)) { result.suspicionDelta = ClampDelta(suspicion); foundAnyTag = true; }

        var topicMatch = s_TopicRegex.Match(tagsBlock);
        if (topicMatch.Success)
        {
            string val = topicMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(val))
            {
                result.topic = val;
                foundAnyTag = true;
            }
        }

        if (foundAnyTag) result.hadStructuredTags = true;

        result.reply = StripStrayTags(replyBlock).Trim();
        if (string.IsNullOrWhiteSpace(result.reply))
            result.reply = StripStrayTags(raw).Trim();

        return result;
    }

    private static bool TryExtractInt(Regex regex, string text, out int value)
    {
        value = 0;
        var m = regex.Match(text);
        if (!m.Success) return false;
        return int.TryParse(m.Groups[1].Value, out value);
    }

    private static int ClampDelta(int v) => Mathf.Clamp(v, -MaxDeltaPerTurn, MaxDeltaPerTurn);

    private static readonly Regex s_StrayTagLine = new Regex(
        @"^\s*(TRUST|AFFECTION|SUSPICION|TOPIC|REPLY)\s*:.*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static string StripStrayTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return s_StrayTagLine.Replace(text, string.Empty);
    }
}
