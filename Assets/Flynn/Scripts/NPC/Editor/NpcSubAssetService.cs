using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class NpcSubAssetService
{
    public const string DefaultConfigFolder = "Assets/Flynn/Configs/NPC";

    public static NpcDialogueAgentConfig CreateNewConfigPacked(string requestedName)
    {
        EnsureFolderChain(DefaultConfigFolder);

        string safe = Sanitize(requestedName);
        string path = AssetDatabase.GenerateUniqueAssetPath(DefaultConfigFolder + "/" + safe + ".asset");

        var config = ScriptableObject.CreateInstance<NpcDialogueAgentConfig>();
        AssetDatabase.CreateAsset(config, path);

        config.personalityProfile = AddSubAsset<NpcPersonalityProfile>(config, safe + "_Personality");
        config.promptTemplate = AddSubAsset<NpcPromptTemplate>(config, safe + "_PromptTemplate");
        config.memorySettings = AddSubAsset<NpcMemorySettings>(config, safe + "_MemorySettings");
        config.knowledge = AddSubAsset<NpcKnowledgeBase>(config, safe + "_Knowledge");
        config.triggers = AddSubAsset<DialogueTriggerSet>(config, safe + "_Triggers");
        config.relationship = AddSubAsset<NpcRelationshipDefaults>(config, safe + "_Relationship");

        if (config.personalityProfile != null)
        {
            config.personalityProfile.displayName = string.IsNullOrWhiteSpace(requestedName) ? safe : requestedName.Trim();
            config.personalityProfile.npcId = "npc." + safe.ToLowerInvariant();
        }

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);

        return config;
    }

    public static void EnsureMissingSubAssets(NpcDialogueAgentConfig config)
    {
        if (config == null) return;
        string path = AssetDatabase.GetAssetPath(config);
        if (string.IsNullOrEmpty(path)) return;

        string baseName = config.name;

        if (config.personalityProfile == null)
            config.personalityProfile = AddSubAsset<NpcPersonalityProfile>(config, baseName + "_Personality");

        if (config.promptTemplate == null)
            config.promptTemplate = AddSubAsset<NpcPromptTemplate>(config, baseName + "_PromptTemplate");

        if (config.memorySettings == null)
            config.memorySettings = AddSubAsset<NpcMemorySettings>(config, baseName + "_MemorySettings");

        if (config.knowledge == null)
            config.knowledge = AddSubAsset<NpcKnowledgeBase>(config, baseName + "_Knowledge");

        if (config.triggers == null)
            config.triggers = AddSubAsset<DialogueTriggerSet>(config, baseName + "_Triggers");

        if (config.relationship == null)
            config.relationship = AddSubAsset<NpcRelationshipDefaults>(config, baseName + "_Relationship");

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);
    }

    public static NpcDialogueAgentConfig DuplicateConfig(NpcDialogueAgentConfig source, string newName)
    {
        if (source == null) return null;
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath)) return null;

        string folder = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(folder)) folder = DefaultConfigFolder;

        string safe = Sanitize(newName);
        string newPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safe + ".asset");

        if (!AssetDatabase.CopyAsset(sourcePath, newPath))
            return null;

        var copy = AssetDatabase.LoadAssetAtPath<NpcDialogueAgentConfig>(newPath);
        if (copy != null && copy.personalityProfile != null)
        {
            copy.personalityProfile.displayName = string.IsNullOrWhiteSpace(newName) ? safe : newName.Trim();
            copy.personalityProfile.npcId = "npc." + safe.ToLowerInvariant();
            EditorUtility.SetDirty(copy.personalityProfile);
        }
        AssetDatabase.SaveAssets();
        return copy;
    }

    public static void DeleteConfig(NpcDialogueAgentConfig config)
    {
        if (config == null) return;
        string path = AssetDatabase.GetAssetPath(config);
        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
    }

    public static List<NpcDialogueAgentConfig> FindAllConfigs()
    {
        var list = new List<NpcDialogueAgentConfig>();
        string[] guids = AssetDatabase.FindAssets("t:NpcDialogueAgentConfig");
        for (int i = 0; i < guids.Length; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<NpcDialogueAgentConfig>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (asset != null) list.Add(asset);
        }
        return list;
    }

    public static List<Topic> FindAllTopics()
    {
        var list = new List<Topic>();
        string[] guids = AssetDatabase.FindAssets("t:Topic");
        for (int i = 0; i < guids.Length; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Topic>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (asset != null) list.Add(asset);
        }
        return list;
    }

    private static T AddSubAsset<T>(NpcDialogueAgentConfig parent, string subName) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        asset.name = subName;
        AssetDatabase.AddObjectToAsset(asset, parent);
        return asset;
    }

    private static string Sanitize(string raw)
    {
        string s = string.IsNullOrWhiteSpace(raw) ? "NewNpc" : raw.Trim();
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++) s = s.Replace(invalid[i], '_');
        s = s.Replace(' ', '_');
        while (s.Contains("__")) s = s.Replace("__", "_");
        s = s.Trim('_');
        return string.IsNullOrWhiteSpace(s) ? "NewNpc" : s;
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
