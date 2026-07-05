using System.Collections.Generic;
using System.Text;


using Flynn.Core;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc
{
    // Single source of truth for the controlled vocabularies the island JSON
    // uses. The validator, the editor dropdowns, and the authoring skill all
    // reference these so they can never drift apart.
    public static class IslandContentVocab
    {
        public static readonly string[] KnowledgeKinds =
        {
            "fact", "belief", "rumor", "secret", "avoid"
        };

        // Categorisation for signals. Listeners match by signalId, so this is
        // primarily for designers and for handlers that want to route on type.
        public static readonly string[] SignalHandlers =
        {
            "RevealLocation", "RevealClue", "UnlockTopic", "UnlockObjective",
            "UpdateObjective", "CompleteObjective", "ChangeWorldState", "ChangeNpcState",
            "GiveItem", "RequestItem", "TutorialHint", "AmbientRemark",
            "SocialReveal", "RelationshipMilestone", "StartActivity",
            "StoryBeat", "ClueReveal", "WorldChange"
        };

        public static bool IsKnowledgeKind(string s) => Contains(KnowledgeKinds, s);
        public static bool IsSignalHandler(string s) => Contains(SignalHandlers, s);

        // IDs are lower_snake_case with optional dots (e.g. npc.maren_wind_keeper).
        // Empty is allowed for callers that treat "" as "untied"; callers that
        // require an id should check separately.
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            foreach (char c in id)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '.';
                if (!ok) return false;
            }
            return true;
        }

        private static bool Contains(string[] set, string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < set.Length; i++)
                if (string.Equals(set[i], s, System.StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public enum ValidationSeverity { Error, Warning }

    public struct ValidationIssue
    {
        public ValidationSeverity severity;
        public string path;     // e.g. "npcs[1].knowledge[2].thingId"
        public string message;

        public override string ToString() =>
            (severity == ValidationSeverity.Error ? "ERROR " : "WARN  ") + path + " — " + message;
    }

    // Validates an IslandContent against the schema rules that JsonUtility can't
    // express: unique ids, valid references, controlled vocab, length limits.
    // Pure — no UnityEditor dependency — so it runs from the editor window AND
    // from execute_code during skill authoring.
    public static class IslandContentValidator
    {
        private const int k_KnowledgeTextMax = 140;
        private const int k_SignalDescMax = 120;

        public static List<ValidationIssue> Validate(IslandContent c)
        {
            var issues = new List<ValidationIssue>();
            if (c == null)
            {
                Add(issues, ValidationSeverity.Error, "root", "Island content is null (parse failed).");
                return issues;
            }

            if (string.IsNullOrWhiteSpace(c.islandId))
                Add(issues, ValidationSeverity.Error, "islandId", "islandId is required.");
            else if (!IslandContentVocab.IsValidId(c.islandId))
                Add(issues, ValidationSeverity.Error, "islandId", "islandId must be lower_snake_case: '" + c.islandId + "'.");

            // Collect thing ids for reference checks.
            var thingIds = new HashSet<string>();
            if (c.things != null)
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < c.things.Count; i++)
                {
                    var t = c.things[i];
                    string p = "things[" + i + "]";
                    if (t == null) { Add(issues, ValidationSeverity.Error, p, "null thing entry."); continue; }
                    CheckId(issues, p + ".thingId", t.thingId, required: true);
                    if (!string.IsNullOrEmpty(t.thingId))
                    {
                        if (!seen.Add(t.thingId))
                            Add(issues, ValidationSeverity.Error, p + ".thingId", "duplicate thingId '" + t.thingId + "'.");
                        thingIds.Add(t.thingId);
                    }
                    if (string.IsNullOrWhiteSpace(t.displayName))
                        Add(issues, ValidationSeverity.Warning, p + ".displayName", "thing has no displayName.");
                    if (t.aliases == null || t.aliases.Count == 0)
                        Add(issues, ValidationSeverity.Warning, p + ".aliases", "no aliases — player text matching will be weak.");
                }
            }

            // Community.
            if (c.community == null)
            {
                Add(issues, ValidationSeverity.Warning, "community", "no community block.");
            }
            else
            {
                CheckKnowledgeList(issues, "community.knowledge", c.community.knowledge, thingIds);
            }

            // Signals.
            if (c.signals != null)
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < c.signals.Count; i++)
                {
                    var s = c.signals[i];
                    string p = "signals[" + i + "]";
                    if (s == null) { Add(issues, ValidationSeverity.Error, p, "null signal entry."); continue; }
                    CheckId(issues, p + ".signalId", s.signalId, required: true);
                    if (!string.IsNullOrEmpty(s.signalId) && !seen.Add(s.signalId))
                        Add(issues, ValidationSeverity.Error, p + ".signalId", "duplicate signalId '" + s.signalId + "'.");
                    CheckThingRef(issues, p + ".thingId", s.thingId, thingIds);
                    if (string.IsNullOrWhiteSpace(s.description))
                        Add(issues, ValidationSeverity.Warning, p + ".description", "signal has no description — the LLM won't know when to fire it.");
                    else if (s.description.Length > k_SignalDescMax)
                        Add(issues, ValidationSeverity.Warning, p + ".description", "over " + k_SignalDescMax + " chars (" + s.description.Length + ").");
                    if (!string.IsNullOrEmpty(s.handler) && !IslandContentVocab.IsSignalHandler(s.handler))
                        Add(issues, ValidationSeverity.Warning, p + ".handler", "'" + s.handler + "' is not a known handler.");
                }
            }

            // NPCs.
            if (c.npcs == null || c.npcs.Count == 0)
            {
                Add(issues, ValidationSeverity.Warning, "npcs", "island has no NPCs.");
            }
            else
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < c.npcs.Count; i++)
                {
                    var n = c.npcs[i];
                    string p = "npcs[" + i + "]";
                    if (n == null) { Add(issues, ValidationSeverity.Error, p, "null npc entry."); continue; }
                    CheckId(issues, p + ".npcId", n.npcId, required: true);
                    if (!string.IsNullOrEmpty(n.npcId) && !seen.Add(n.npcId))
                        Add(issues, ValidationSeverity.Error, p + ".npcId", "duplicate npcId '" + n.npcId + "'.");
                    if (string.IsNullOrWhiteSpace(n.displayName))
                        Add(issues, ValidationSeverity.Warning, p + ".displayName", "npc has no displayName.");
                    if (string.IsNullOrWhiteSpace(n.role))
                        Add(issues, ValidationSeverity.Warning, p + ".role", "npc has no role.");
                    int traits = n.personalityTraits != null ? n.personalityTraits.Count : 0;
                    if (traits < 3 || traits > 5)
                        Add(issues, ValidationSeverity.Warning, p + ".personalityTraits", "expected 3-5 traits, got " + traits + ".");
                    if (n.knowledge == null || n.knowledge.Count == 0)
                        Add(issues, ValidationSeverity.Warning, p + ".knowledge", "npc has no knowledge — nothing for the player to learn here.");
                    CheckKnowledgeList(issues, p + ".knowledge", n.knowledge, thingIds);
                }
            }

            return issues;
        }

        public static string Summarize(List<ValidationIssue> issues)
        {
            int errors = 0, warnings = 0;
            foreach (var i in issues)
                if (i.severity == ValidationSeverity.Error) errors++; else warnings++;
            var sb = new StringBuilder();
            sb.Append(errors).Append(" error").Append(errors == 1 ? "" : "s")
              .Append(", ").Append(warnings).Append(" warning").Append(warnings == 1 ? "" : "s");
            return sb.ToString();
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static void CheckKnowledgeList(List<ValidationIssue> issues, string basePath, List<KnowledgeContent> list, HashSet<string> thingIds)
        {
            if (list == null) return;
            var seen = new HashSet<string>();
            for (int i = 0; i < list.Count; i++)
            {
                var k = list[i];
                string p = basePath + "[" + i + "]";
                if (k == null) { Add(issues, ValidationSeverity.Error, p, "null knowledge entry."); continue; }
                CheckId(issues, p + ".id", k.id, required: true);
                if (!string.IsNullOrEmpty(k.id) && !seen.Add(k.id))
                    Add(issues, ValidationSeverity.Error, p + ".id", "duplicate knowledge id '" + k.id + "'.");
                CheckThingRef(issues, p + ".thingId", k.thingId, thingIds);
                if (!IslandContentVocab.IsKnowledgeKind(k.kind))
                    Add(issues, ValidationSeverity.Error, p + ".kind", "'" + k.kind + "' is not a valid kind (fact|belief|rumor|secret|avoid).");
                if (string.IsNullOrWhiteSpace(k.text))
                    Add(issues, ValidationSeverity.Error, p + ".text", "empty knowledge text.");
                else if (k.text.Length > k_KnowledgeTextMax)
                    Add(issues, ValidationSeverity.Warning, p + ".text", "over " + k_KnowledgeTextMax + " chars (" + k.text.Length + ").");
            }
        }

        private static void CheckId(List<ValidationIssue> issues, string path, string id, bool required)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                if (required) Add(issues, ValidationSeverity.Error, path, "id is required.");
                return;
            }
            if (!IslandContentVocab.IsValidId(id))
                Add(issues, ValidationSeverity.Error, path, "must be lower_snake_case: '" + id + "'.");
        }

        private static void CheckThingRef(List<ValidationIssue> issues, string path, string thingId, HashSet<string> thingIds)
        {
            if (string.IsNullOrEmpty(thingId)) return; // untied is allowed
            if (!thingIds.Contains(thingId))
                Add(issues, ValidationSeverity.Error, path, "references unknown thingId '" + thingId + "'.");
        }

        private static void Add(List<ValidationIssue> issues, ValidationSeverity sev, string path, string msg)
        {
            issues.Add(new ValidationIssue { severity = sev, path = path, message = msg });
        }
    }

}
