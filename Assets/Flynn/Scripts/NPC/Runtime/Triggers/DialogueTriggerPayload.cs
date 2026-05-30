public struct DialogueTriggerPayload
{
    public DialogueTriggerDef trigger;
    public NpcDialogueAgentConfig sourceNpc;
    public string topic;
    public int trustDelta;
    public int affectionDelta;
    public int suspicionDelta;

    // Stable string id used by save data, LLM, and logs. Derived from the def's asset name.
    public string Key => trigger != null ? trigger.Key : null;
}
