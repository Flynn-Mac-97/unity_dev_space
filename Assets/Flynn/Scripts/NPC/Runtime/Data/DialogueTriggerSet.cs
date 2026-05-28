using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Dialogue Trigger Set", fileName = "DialogueTriggerSet")]
public class DialogueTriggerSet : ScriptableObject
{
    public enum TriggerKind
    {
        StoryBeat,
        ClueReveal,
        OneTime,
        Repeatable,
        Forbidden,
        Secret,
        Misdirection,
    }

    [Serializable]
    public class DialogueTrigger
    {
        [Tooltip("Stable key used in save data. Lower case, dot delimited. Example: trigger.cave_unlocked")]
        public string key = "trigger.new";

        public Topic topic;

        public TriggerKind kind = TriggerKind.Repeatable;

        [TextArea(2, 5)]
        [Tooltip("Author-facing description of what the NPC says or what happens. The runtime uses this as guidance for the LLM.")]
        public string text = "";

        [Tooltip("Editor-only. Lets designers mark triggers that should not appear in the prompt yet (work in progress).")]
        public bool draft = false;
    }

    public List<DialogueTrigger> triggers = new List<DialogueTrigger>();
}
