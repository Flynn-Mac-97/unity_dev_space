using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Trigger Definition", fileName = "trigger_new")]
public class DialogueTriggerDef : ScriptableObject
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

    public TriggerKind kind = TriggerKind.Repeatable;

    public Topic topic;

    [TextArea(2, 5)]
    [Tooltip("Author-facing description of what the NPC says or what happens. The runtime uses this as guidance for the LLM.")]
    public string description = "";

    [Tooltip("Editor-only. Lets designers mark triggers that should not appear in the prompt yet (work in progress).")]
    public bool draft = false;

    // Stable runtime key. Asset filename — never typed by hand, can't typo.
    public string Key => name;
}
