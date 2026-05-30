using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Trigger Channel", fileName = "DialogueTriggers")]
public class DialogueTriggerChannel : ScriptableObject
{
    public event Action<DialogueTriggerPayload> OnRaised;

    public void Raise(DialogueTriggerPayload payload)
    {
        if (payload.trigger == null) return;
        OnRaised?.Invoke(payload);
    }

    public void Raise(DialogueTriggerDef trigger, NpcDialogueAgentConfig sourceNpc)
    {
        Raise(new DialogueTriggerPayload { trigger = trigger, sourceNpc = sourceNpc, topic = "none" });
    }
}
