using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/NPC Dialogue Agent Config")]
public class NpcDialogueAgentConfig : ScriptableObject
{
    [Header("Required Assets")]
    public NpcPersonalityProfile personalityProfile;

    [Header("Prompt Template")]
    [Tooltip("Per-NPC system prompt template. Authored in the Dashboard tab. Supports {tokens} like {name}, {role}, {known_facts}, {triggers}, {relationship} — substituted from the other tabs at runtime. Appended to the global system prompt on SceneLlmManager.")]
    [TextArea(6, 30)]
    public string promptTemplate = DefaultPromptTemplate;

    public const string DefaultPromptTemplate =
        "You are {name}. {role}\n" +
        "\n" +
        "How you speak: {speaking_style}\n" +
        "Personality: {personality_traits}\n" +
        "\n" +
        "Do: {do}\n" +
        "Don't: {dont}\n" +
        "\n" +
        "Things you can actually do in this game (do NOT offer anything outside this list):\n" +
        "{capabilities}\n" +
        "\n" +
        "What you know to be true:\n" +
        "{known_facts}\n" +
        "\n" +
        "What you believe (you may be wrong):\n" +
        "{beliefs}\n" +
        "\n" +
        "Rumors you've heard:\n" +
        "{rumors}\n" +
        "\n" +
        "Secrets you only share when you trust someone:\n" +
        "{secrets}\n" +
        "\n" +
        "Topics you avoid or deflect from: {avoided_topics}\n" +
        "\n" +
        "Your starting feelings toward the player: {relationship}";

    [Header("Gameplay")]
    [Tooltip("Which gameplay functions this NPC fulfills. Drives validator and prompt assembly.")]
    public NpcGameplayRoles roles = NpcGameplayRoles.Villager;

    [Header("Knowledge and triggers")]
    public NpcKnowledgeBase knowledge;

    [Tooltip("Trigger definitions this NPC may fire mid-dialogue. Drag DialogueTriggerDef assets here. Scene listeners reference the same assets.")]
    public List<DialogueTriggerDef> triggers = new List<DialogueTriggerDef>();

    [Header("Relationship")]
    public NpcRelationshipDefaults relationship;

    [Header("Behavior")]
    [Tooltip("When true, use local model when available. Otherwise rely on fallback text.")]
    public bool useLocalModel = true;

    [Tooltip("Use this as deterministic fallback if model call fails.")]
    [TextArea(2, 5)]
    public string fallbackReply = "I am having trouble thinking right now, but I am still listening.";

    public bool HasRequiredReferences()
    {
        return personalityProfile != null;
    }

    public bool HasGameplaySpine()
    {
        return knowledge != null && triggers != null && triggers.Count > 0 && relationship != null;
    }
}
