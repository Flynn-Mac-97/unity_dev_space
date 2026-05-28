using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/NPC Knowledge Base", fileName = "NpcKnowledgeBase")]
public class NpcKnowledgeBase : ScriptableObject
{
    public enum RevealCondition
    {
        Always,
        TrustAtLeast,
        AffectionAtLeast,
        SuspicionAtMost,
        FlagSet,
    }

    [Serializable]
    public class KnowledgeEntry
    {
        public Topic topic;

        [TextArea(2, 5)]
        public string text = "";

        [Tooltip("If true, this entry is consumed the first time it is revealed in conversation.")]
        public bool oneTime = false;

        public RevealCondition reveal = RevealCondition.Always;

        [Tooltip("Used by TrustAtLeast / AffectionAtLeast / SuspicionAtMost (0-100).")]
        [Range(0, 100)]
        public int threshold = 0;

        [Tooltip("Used by FlagSet. Save-data flag key the dialogue runtime must have set.")]
        public string flagKey = "";
    }

    [Header("Knowledge buckets")]
    [Tooltip("Things this NPC knows to be true and will state plainly.")]
    public List<KnowledgeEntry> knownFacts = new List<KnowledgeEntry>();

    [Tooltip("Things this NPC believes but may be wrong about. The world may contradict these.")]
    public List<KnowledgeEntry> beliefs = new List<KnowledgeEntry>();

    [Tooltip("Hearsay this NPC trusts enough to repeat. Useful for misdirection.")]
    public List<KnowledgeEntry> rumors = new List<KnowledgeEntry>();

    [Tooltip("Information revealed only under conditions (trust, flag, etc.).")]
    public List<KnowledgeEntry> secrets = new List<KnowledgeEntry>();

    [Header("Avoidance")]
    [Tooltip("Topics this NPC will refuse to discuss or will deflect from.")]
    public List<Topic> avoidedTopics = new List<Topic>();
}
