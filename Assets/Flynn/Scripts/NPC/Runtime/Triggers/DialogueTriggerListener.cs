using UnityEngine;
using UnityEngine.Events;



namespace Flynn.Npc
{
    public class DialogueTriggerListener : MonoBehaviour
    {
        [Header("Channel")]
        [Tooltip("ScriptableObject bus this listener subscribes to.")]
        public DialogueTriggerChannel channel;

        [Header("Filter")]
        [Tooltip("Signal id this listener reacts to (must match a SignalContent.signalId in the island JSON).")]
        public string requiredTriggerKey;

        [Tooltip("Leave empty to accept signals from any NPC. Otherwise match against the firing NPC's id.")]
        public string requiredNpcId;

        [Tooltip("Unsubscribe after the first fire.")]
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
            if (string.IsNullOrWhiteSpace(requiredTriggerKey)) return;
            if (payload.triggerKey != requiredTriggerKey) return;
            if (!string.IsNullOrEmpty(requiredNpcId) && payload.sourceNpcId != requiredNpcId) return;

            _fired = true;
            onTriggered?.Invoke();
        }
    }

}
