using UnityEditor;
using UnityEngine;

public static class DemoNpcBuilder
{
    private const string TopicsFolder = "Assets/Flynn/Configs/NPC/Topics";

    [MenuItem("Tools/Dialogue/Create Demo NPC (Maren the Wind Keeper)")]
    public static void CreateDemoNpc()
    {
        EnsureFolderChain(TopicsFolder);

        Topic caveNorth = GetOrCreateTopic("cave_north", "The North Cave", Topic.Category.Place,
            "Sealed cave on the first island. Old story says wind opens it.");
        Topic windTunnel = GetOrCreateTopic("wind_tunnel", "Sky Tunnel", Topic.Category.Place,
            "A current in the air between the two largest islands.");
        Topic technicianJorin = GetOrCreateTopic("npc_jorin", "Jorin the Technician", Topic.Category.Person,
            "Lives two islands south. Builds wind pumps.");
        Topic windPumpShard = GetOrCreateTopic("item_pump_shard", "Pump Shard", Topic.Category.Item,
            "Tech scrap from broken wind pumps. Jorin pays well for these.");
        Topic dangerousMemory = GetOrCreateTopic("rumor_storm_year", "The Storm Year", Topic.Category.Rumor,
            "Old rumor about the year the sky split.");

        var config = NpcSubAssetService.CreateNewConfigPacked("Maren_WindKeeper");

        var profile = config.personalityProfile;
        profile.displayName = "Maren";
        profile.npcId = "npc.maren_wind_keeper";
        profile.roleDescription = "An old keeper of a wind shrine on the first island. Knows the air currents better than anyone.";
        profile.speakingStyle = "Short sentences. Calm. Uses weather and bird metaphors.";
        profile.personalityTraits = "Patient, watchful, slightly proud of her knowledge.";
        profile.doRules = "Talk about wind, birds, and small daily things. Mention the shrine when it fits. Stay in character.";
        profile.dontRules = "Do not mention being an AI. Do not spoil the cave's secret unless trust is high.";
        profile.fallbackLines = new[]
        {
            "The wind is loud today. Say that again.",
            "Hm. Let me listen for a moment.",
            "I am not sure I caught that, traveler.",
        };

        config.roles = NpcGameplayRoles.ClueGiver | NpcGameplayRoles.LoreKeeper | NpcGameplayRoles.Villager;

        var k = config.knowledge;
        k.knownFacts.Add(MakeEntry(windTunnel, "There is a steady current between the two big islands. A small boat is fastest at dawn."));
        k.knownFacts.Add(MakeEntry(technicianJorin, "Jorin lives south. He fixes anything that turns or blows."));

        k.beliefs.Add(MakeEntry(dangerousMemory, "The sky split the year I was born. Half the village still says so. I think it was only a bad storm."));

        k.rumors.Add(MakeEntryGated(caveNorth,
            "Travelers whisper that the north cave is sealed by a song. Or by a tool. I forget which.",
            NpcKnowledgeBase.RevealCondition.TrustAtLeast, 40));

        k.secrets.Add(MakeEntryGated(caveNorth,
            "The cave opens for whoever stands at the mouth when the sky tunnel blows north. Jorin's pumps can hold the door for one breath.",
            NpcKnowledgeBase.RevealCondition.TrustAtLeast, 75));

        k.avoidedTopics.Add(dangerousMemory);

        var t = config.triggers;
        t.triggers.Add(new DialogueTriggerSet.DialogueTrigger
        {
            key = "trigger.maren.first_meeting",
            topic = null,
            kind = DialogueTriggerSet.TriggerKind.OneTime,
            text = "First time the player meets Maren she comments on the wind direction and asks where they came from.",
        });
        t.triggers.Add(new DialogueTriggerSet.DialogueTrigger
        {
            key = "trigger.maren.clue_cave_north",
            topic = caveNorth,
            kind = DialogueTriggerSet.TriggerKind.ClueReveal,
            text = "Maren mentions a sealed cave to the north when the player brings up old places or asks about secrets.",
        });
        t.triggers.Add(new DialogueTriggerSet.DialogueTrigger
        {
            key = "trigger.maren.secret_cave_method",
            topic = caveNorth,
            kind = DialogueTriggerSet.TriggerKind.Secret,
            text = "If trust is high enough, Maren tells the player how the cave actually opens (sky tunnel + a wind pump).",
        });
        t.triggers.Add(new DialogueTriggerSet.DialogueTrigger
        {
            key = "trigger.maren.forbid_storm_year",
            topic = dangerousMemory,
            kind = DialogueTriggerSet.TriggerKind.Forbidden,
            text = "Maren refuses to discuss the Storm Year directly. She changes the subject to birds or weather.",
        });

        var r = config.relationship;
        r.startingTrust = 35;
        r.startingAffection = 40;
        r.startingSuspicion = 15;
        r.initialPlayerStatus = NpcRelationshipDefaults.PlayerStatusTag.Unknown;
        r.trustToShareClues = 40;
        r.trustToShareSecrets = 75;

        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(k);
        EditorUtility.SetDirty(t);
        EditorUtility.SetDirty(r);
        AssetDatabase.SaveAssets();

        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);

        NpcAuthoringStudioWindow.OpenWithConfig(config);

        Debug.Log("Demo NPC created: " + AssetDatabase.GetAssetPath(config));
    }

    private static NpcKnowledgeBase.KnowledgeEntry MakeEntry(Topic topic, string text)
    {
        return new NpcKnowledgeBase.KnowledgeEntry { topic = topic, text = text };
    }

    private static NpcKnowledgeBase.KnowledgeEntry MakeEntryGated(Topic topic, string text, NpcKnowledgeBase.RevealCondition reveal, int threshold)
    {
        return new NpcKnowledgeBase.KnowledgeEntry { topic = topic, text = text, reveal = reveal, threshold = threshold };
    }

    private static Topic GetOrCreateTopic(string topicId, string displayName, Topic.Category category, string description)
    {
        string[] guids = AssetDatabase.FindAssets("t:Topic");
        for (int i = 0; i < guids.Length; i++)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Topic>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (existing != null && existing.topicId == topicId)
                return existing;
        }

        var t = ScriptableObject.CreateInstance<Topic>();
        t.topicId = topicId;
        t.displayName = displayName;
        t.category = category;
        t.description = description;
        string path = AssetDatabase.GenerateUniqueAssetPath(TopicsFolder + "/Topic_" + topicId + ".asset");
        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    private static void EnsureFolderChain(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string[] parts = folder.Split('/');
        string running = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = running + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(running, parts[i]);
            running = next;
        }
    }
}
