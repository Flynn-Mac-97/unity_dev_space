
using Flynn.Core;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Npc
{
    public struct DialogueTriggerPayload
    {
        /// Signal id (matches a SignalContent.signalId in the island JSON).
        public string triggerKey;
        /// Id of the NPC that fired it. Empty when fired from a non-dialogue source.
        public string sourceNpcId;
        /// Handler category from the authored signal (IslandContentVocab.SignalHandlers).
        /// Optional — listeners usually match on triggerKey, but may route on this.
        public string handler;
        /// Opaque payload string from the authored signal (usually JSON). Delivered
        /// verbatim so a handler can interpret it.
        public string payloadJson;
        public string topic;
        public int trustDelta;

        public string Key => triggerKey;
    }

}
