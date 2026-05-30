using UnityEngine;
using UnityEngine.Events;

public class DialogueTriggerListener : MonoBehaviour
{
    [Header("Channel")]
    [Tooltip("ScriptableObject bus this listener subscribes to. Drag in the project's DialogueTriggers asset.")]
    public DialogueTriggerChannel channel;

    [Header("Filter")]
    [Tooltip("The trigger definition this listener reacts to. Drag the same DialogueTriggerDef asset the NPC config references.")]
    public DialogueTriggerDef requiredTrigger;

    [Tooltip("Leave null to accept triggers from any NPC. Set to scope this listener to one NPC's config.")]
    public NpcDialogueAgentConfig requiredNpc;

    [Tooltip("Unsubscribe after the first fire. Local to this scene instance — reload clears it.")]
    public bool oneShot;

    [Header("Reaction")]
    public UnityEvent onTriggered;

    private bool _fired;

    private void OnEnable()
    {
        if (channel == null) return;
        channel.OnRaised += HandleRaised;
    }

    private void OnDisable()
    {
        if (channel == null) return;
        channel.OnRaised -= HandleRaised;
    }

    private void HandleRaised(DialogueTriggerPayload payload)
    {
        if (_fired && oneShot) return;
        if (requiredTrigger == null) return;
        if (payload.trigger != requiredTrigger) return;
        if (requiredNpc != null && payload.sourceNpc != requiredNpc) return;

        _fired = true;
        onTriggered?.Invoke();
    }
}
