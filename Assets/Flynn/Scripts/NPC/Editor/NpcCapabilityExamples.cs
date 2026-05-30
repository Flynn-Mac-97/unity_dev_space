using System.Collections.Generic;

// Role-keyed library of example "things I can do" capabilities. Surfaced in the
// Identity tab as click-to-add chips so designers can quickly populate the
// NpcPersonalityProfile.capabilities list based on the NPC's gameplay roles.
public static class NpcCapabilityExamples
{
    private static readonly Dictionary<NpcGameplayRoles, string[]> s_byRole =
        new Dictionary<NpcGameplayRoles, string[]>
        {
            { NpcGameplayRoles.Merchant, new[]
                {
                    "Give the player an item from my stock",
                    "Buy an item from the player for currency",
                    "Quote a price for goods I sell",
                    "Refuse to trade with someone I distrust",
                } },
            { NpcGameplayRoles.ClueGiver, new[]
                {
                    "Point the player to a specific location on the map",
                    "Mention another character they can go meet",
                    "Confirm or deny something the player has seen",
                    "Hand the player a written note or map",
                } },
            { NpcGameplayRoles.LoreKeeper, new[]
                {
                    "Recount a past event from this region",
                    "Explain the meaning of a symbol, place, or name",
                    "Cite an old saying or song",
                } },
            { NpcGameplayRoles.Technician, new[]
                {
                    "Repair a broken item the player brings",
                    "Diagnose a malfunctioning device",
                    "Upgrade equipment if given the right parts",
                } },
            { NpcGameplayRoles.Storyteller, new[]
                {
                    "Tell a story when asked",
                    "Sing a short song",
                    "Pause for dramatic effect",
                } },
            { NpcGameplayRoles.Guardian, new[]
                {
                    "Block the player from passing without permission",
                    "Allow passage once a condition is met",
                    "Warn of danger ahead",
                } },
            { NpcGameplayRoles.Scammer, new[]
                {
                    "Offer a deal that sounds too good",
                    "Lie about an item's value or origin",
                    "Vanish if the player presses too hard",
                } },
            { NpcGameplayRoles.Child, new[]
                {
                    "Run away if startled",
                    "Ask the player to play a game",
                    "Share a secret they overheard from an adult",
                } },
            { NpcGameplayRoles.BrokenRobot, new[]
                {
                    "Glitch mid-sentence and repeat a word",
                    "Request a specific part the player might be carrying",
                    "Reboot after answering a difficult question",
                } },
            { NpcGameplayRoles.Villager, new[]
                {
                    "Point the player toward a nearby place",
                    "Comment on the weather or local gossip",
                    "Mention what other villagers are doing today",
                } },
            { NpcGameplayRoles.Other, new[]
                {
                    "Greet the player",
                    "Wave goodbye when the conversation ends",
                    "Comment on what the player is carrying",
                } },
        };

    private static readonly string[] s_fallback =
    {
        "Greet the player",
        "Answer questions in character",
        "Wave goodbye when the conversation ends",
    };

    // Returns the union of examples for every role flag set on the NPC,
    // de-duplicated, with already-added capabilities filtered out.
    public static List<string> SuggestionsFor(NpcGameplayRoles roles, List<string> alreadyAdded)
    {
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (alreadyAdded != null)
            for (int i = 0; i < alreadyAdded.Count; i++)
                if (!string.IsNullOrWhiteSpace(alreadyAdded[i])) seen.Add(alreadyAdded[i].Trim());

        var result = new List<string>();
        bool anyRole = false;
        foreach (NpcGameplayRoles v in System.Enum.GetValues(typeof(NpcGameplayRoles)))
        {
            if (v == NpcGameplayRoles.None) continue;
            if ((roles & v) != v) continue;
            anyRole = true;
            if (!s_byRole.TryGetValue(v, out string[] examples)) continue;
            for (int i = 0; i < examples.Length; i++)
                if (seen.Add(examples[i])) result.Add(examples[i]);
        }

        if (!anyRole)
        {
            for (int i = 0; i < s_fallback.Length; i++)
                if (seen.Add(s_fallback[i])) result.Add(s_fallback[i]);
        }

        return result;
    }
}
