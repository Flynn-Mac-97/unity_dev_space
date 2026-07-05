using System;
using UnityEngine;



namespace Flynn.Npc
{
    [CreateAssetMenu(menuName = "Dialogue/Trigger Channel", fileName = "DialogueTriggers")]
    public class DialogueTriggerChannel : ScriptableObject
    {
        public event Action<DialogueTriggerPayload> OnRaised;

        public void Raise(DialogueTriggerPayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.triggerKey)) return;
            OnRaised?.Invoke(payload);
        }

        public void Raise(string triggerKey, string sourceNpcId)
            => Raise(triggerKey, sourceNpcId, null, null);

        public void Raise(string triggerKey, string sourceNpcId, string handler, string payloadJson)
        {
            Raise(new DialogueTriggerPayload
            {
                triggerKey = triggerKey,
                sourceNpcId = sourceNpcId ?? string.Empty,
                handler = handler ?? string.Empty,
                payloadJson = payloadJson ?? string.Empty,
                topic = "none",
            });
        }
    }

}
