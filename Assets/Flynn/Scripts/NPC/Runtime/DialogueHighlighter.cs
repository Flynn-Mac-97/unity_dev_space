using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Flynn.Npc
{
    // Scans NPC reply text for known thing names/aliases and wraps them in rich
    // text <color> tags so the player can visually identify game-relevant terms.
    //
    // Highlight categories:
    //   Thing reference  — warm gold (#FFD700) — known world things (transmitter, solar collector, etc.)
    //   Topic keyword     — light gold (#FFE066) — the envelope.topic if it appears in the reply
    //
    // Usage: DialogueManager calls Highlight(reply, topic, hub) after the typewriter
    // routine completes. The Label's enableRichText must be true (set in TryBindUi).
    public static class DialogueHighlighter
    {
        // Hex colors for rich text <color> tags.
        public const string ThingColor = "#FFD700";
        public const string TopicColor = "#FFE066";

        // Cache of thing terms, built once per dialogue session.
        private static List<TermEntry> _termCache;
        private static string _cacheIslandId;

        // Last set of match ranges from the most recent Highlight() call.
        // Used by GetTermAtClick to map mouse position → clicked term.
        private static List<MatchRange> _lastMatchRanges;
        private static string _lastPlainText;

        public struct MatchRange
        {
            public int Start;  // position in plain text
            public int Length;
            public string Term;
        }

        // One search term with its associated thing data.
        private struct TermEntry
        {
            public string LowerTerm;     // lowercase for matching
            public string DisplayTerm;    // original casing for replacement
            public string ThingId;
            public int Length;            // for longest-match-first ordering
        }

        // Rebuilds the term cache from the current island content. Called when
        // a dialogue opens (or on first use). Safe to call repeatedly — skips
        // if the island hasn't changed.
        public static void BuildTermCache(IslandContentHub hub)
        {
            if (hub == null)
            {
                Debug.LogWarning("[DialogueHighlighter] BuildTermCache: hub is null");
                return;
            }
            if (!hub.IsLoaded)
            {
                Debug.LogWarning("[DialogueHighlighter] BuildTermCache: hub not loaded");
                return;
            }

            var content = hub.Content;
            if (content == null)
            {
                Debug.LogWarning("[DialogueHighlighter] BuildTermCache: hub.Content is null");
                return;
            }

            // Skip rebuild if same island.
            if (_termCache != null && _cacheIslandId == content.islandId)
            {
                Debug.Log($"[DialogueHighlighter] Term cache already built for island '{content.islandId}' ({_termCache.Count} terms)");
                return;
            }

            _cacheIslandId = content.islandId;
            _termCache = new List<TermEntry>();

            if (content.things == null)
            {
                Debug.LogWarning("[DialogueHighlighter] BuildTermCache: content.things is null");
                return;
            }

            foreach (var thing in content.things)
            {
                if (thing == null) continue;

                // Add the display name as a term.
                AddTerm(thing.displayName, thing.thingId);

                // Add all aliases.
                if (thing.aliases != null)
                    foreach (var alias in thing.aliases)
                        AddTerm(alias, thing.thingId);
            }

            Debug.Log($"[DialogueHighlighter] Term cache built: {_termCache.Count} terms for island '{content.islandId}'");
            foreach (var t in _termCache)
                Debug.Log($"[DialogueHighlighter]   term='{t.DisplayTerm}' thingId='{t.ThingId}'");

            // Sort longest-first so "solar collector" matches before "solar".
            _termCache.Sort((a, b) => b.Length.CompareTo(a.Length));
        }

        private static void AddTerm(string term, string thingId)
        {
            if (string.IsNullOrWhiteSpace(term)) return;
            string trimmed = term.Trim();
            if (trimmed.Length < 3) return; // skip very short terms like "the"

            _termCache.Add(new TermEntry
            {
                LowerTerm = trimmed.ToLowerInvariant(),
                DisplayTerm = trimmed,
                ThingId = thingId,
                Length = trimmed.Length,
            });
        }

        // Returns the reply text with known terms wrapped in <color> rich text tags.
        // If the term cache is empty or the reply is null/empty, returns the original text.
        public static string Highlight(string reply, string topic, IslandContentHub hub)
        {
            if (string.IsNullOrWhiteSpace(reply)) return reply;

            // Strip markdown that the LLM sometimes adds despite instructions.
            reply = StripMarkdown(reply);

            BuildTermCache(hub);
            if (_termCache == null || _termCache.Count == 0) return reply;

            // Collect all matches with their positions, then build the result
            // string left-to-right so overlaps don't double-wrap.
            string lower = reply.ToLowerInvariant();
            var matches = new List<Match>();

            // Find thing references.
            foreach (var term in _termCache)
            {
                int idx = 0;
                while ((idx = lower.IndexOf(term.LowerTerm, idx, System.StringComparison.Ordinal)) >= 0)
                {
                    // Avoid matching inside a word (e.g., "station" inside "transmission").
                    if (IsWordBoundary(lower, idx, term.LowerTerm.Length))
                    {
                        matches.Add(new Match
                        {
                            Start = idx,
                            Length = term.LowerTerm.Length,
                            Color = ThingColor,
                            Term = term.DisplayTerm,
                        });
                    }
                    idx += term.LowerTerm.Length;
                }
            }

            // Find topic keyword if present.
            if (!string.IsNullOrWhiteSpace(topic))
            {
                string lowerTopic = topic.Trim().ToLowerInvariant();
                if (lowerTopic.Length >= 3)
                {
                    int idx = 0;
                    while ((idx = lower.IndexOf(lowerTopic, idx, System.StringComparison.Ordinal)) >= 0)
                    {
                        if (IsWordBoundary(lower, idx, lowerTopic.Length))
                        {
                            matches.Add(new Match
                            {
                                Start = idx,
                                Length = lowerTopic.Length,
                                Color = TopicColor,
                                Term = topic.Trim(),
                            });
                        }
                        idx += lowerTopic.Length;
                    }
                }
            }

            if (matches.Count == 0)
            {
                Debug.Log($"[DialogueHighlighter] No matches found in reply ({reply.Length} chars, {_termCache.Count} terms in cache)");
                return reply;
            }

            Debug.Log($"[DialogueHighlighter] Found {matches.Count} matches in reply");

            // Sort by start position. Remove overlaps (keep the first/longest).
            matches.Sort((a, b) => a.Start.CompareTo(b.Start));

            var sb = new StringBuilder(reply.Length + matches.Count * 20);
            int pos = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                if (m.Start < pos) continue; // overlaps a previous match

                // Append text before the match.
                if (m.Start > pos) sb.Append(reply, pos, m.Start - pos);

                // Append the highlighted term. Unity 2022.3 UI Toolkit rich text:
                // <color=#RRGGBB> without quotes. Closing tag is </color>.
                sb.Append("<color=").Append(m.Color).Append('>');
                sb.Append(reply, m.Start, m.Length);
                sb.Append("</color>");

                pos = m.Start + m.Length;
            }

            // Append remaining text.
            if (pos < reply.Length) sb.Append(reply, pos, reply.Length - pos);

            // Store match ranges for click detection (plain text positions).
            _lastPlainText = reply;
            _lastMatchRanges = new List<MatchRange>();
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                if (m.Start < pos && i > 0) continue; // skip overlaps
                _lastMatchRanges.Add(new MatchRange
                {
                    Start = m.Start,
                    Length = m.Length,
                    Term = m.Term,
                });
            }

            return sb.ToString();
        }

        // Checks that the match at [idx, idx+len) is bounded by non-alphanumeric
        // characters (or string start/end) so we don't highlight substrings of
        // larger words.
        private static bool IsWordBoundary(string text, int idx, int len)
        {
            if (idx > 0 && char.IsLetterOrDigit(text[idx - 1])) return false;
            int end = idx + len;
            if (end < text.Length && char.IsLetterOrDigit(text[end])) return false;
            return true;
        }

        private struct Match
        {
            public int Start;
            public int Length;
            public string Color;
            public string Term;
        }

        // Removes common markdown formatting that LLMs sometimes leak into replies.
        private static string StripMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // **bold** → bold
            text = text.Replace("**", "");
            // *italic* → italic (but only single * not **)
            // Do this carefully — remove single asterisks that aren't part of **
            // Since we already removed **, remaining * are single italic markers
            text = text.Replace("*", "");
            // _underline_ → underline (only when surrounding words, not inside)
            // Keep it simple — just strip leading/trailing _ on words
            return text;
        }

        // Clears the cache (called when dialogue closes or island changes).
        public static void ClearCache()
        {
            _termCache = null;
            _cacheIslandId = null;
            _lastMatchRanges = null;
            _lastPlainText = null;
        }

        // Given a click position on the dialogue label, estimate which term was clicked.
        // Returns the display term, or null if no term was at that position.
        // 
        // Approach: UI Toolkit Label doesn't expose per-character hit boxes, so we
        // estimate the character index from the mouse X position relative to the
        // label's content rect and an estimated character width.
        public static string GetTermAtClick(Vector2 localMousePos, VisualElement label)
        {
            if (_lastMatchRanges == null || _lastMatchRanges.Count == 0) return null;
            if (_lastPlainText == null) return null;
            if (label == null) return null;

            // Get the label's content area (inside padding).
            var contentRect = label.layout;
            float padding = 4f; // matches the USS padding on .dialogue-text
            float textStartX = contentRect.x + padding;
            float textWidth = contentRect.width - padding * 2;

            // Estimate character width based on font size and text length.
            // Unity UI Toolkit doesn't give us per-char metrics, so we approximate.
            float fontSize = label.resolvedStyle.fontSize > 0 ? label.resolvedStyle.fontSize : 20f;
            // Rough estimate: average char width is ~55% of font size for most fonts
            float estCharWidth = fontSize * 0.55f;

            // Calculate approximate character index from mouse X
            float relX = localMousePos.x - textStartX;
            if (relX < 0) return null;

            // Account for text wrapping: estimate which line we're on
            float lineHeight = fontSize * 1.2f;
            float relY = localMousePos.y - contentRect.y - padding;
            if (relY < 0) return null;

            int charsPerLine = Mathf.Max(1, (int)(textWidth / estCharWidth));
            int lineIndex = Mathf.FloorToInt(relY / lineHeight);
            int charInLine = Mathf.FloorToInt(relX / estCharWidth);
            int charIndex = lineIndex * charsPerLine + charInLine;

            if (charIndex < 0 || charIndex >= _lastPlainText.Length) return null;

            // Find word boundaries around the clicked character
            int wordStart = charIndex;
            while (wordStart > 0 && !char.IsWhiteSpace(_lastPlainText[wordStart - 1]))
                wordStart--;
            int wordEnd = charIndex;
            while (wordEnd < _lastPlainText.Length && !char.IsWhiteSpace(_lastPlainText[wordEnd]))
                wordEnd++;

            string clickedWord = _lastPlainText.Substring(wordStart, wordEnd - wordStart).Trim();

            // Check if the clicked position falls within any match range
            foreach (var range in _lastMatchRanges)
            {
                // Check if the clicked character is within this match range
                if (charIndex >= range.Start && charIndex < range.Start + range.Length)
                {
                    Debug.Log($"[DialogueHighlighter] Click matched term: '{range.Term}' at char {charIndex} (range {range.Start}-{range.Start + range.Length})");
                    return range.Term;
                }
            }

            // Fallback: check if the clicked word matches any known term
            if (!string.IsNullOrEmpty(clickedWord))
            {
                string lowerWord = clickedWord.ToLowerInvariant();
                foreach (var range in _lastMatchRanges)
                {
                    if (range.Term.ToLowerInvariant() == lowerWord)
                    {
                        Debug.Log($"[DialogueHighlighter] Click matched term by word: '{range.Term}' (clicked '{clickedWord}')");
                        return range.Term;
                    }
                }
            }

            return null;
        }
    }
}
