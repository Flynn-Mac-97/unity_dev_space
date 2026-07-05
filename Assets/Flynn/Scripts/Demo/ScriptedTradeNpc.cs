using UnityEngine;
using UnityEngine.Events;
using Flynn.Npc;
using Flynn.Player;

namespace Flynn.Demo
{
    /// <summary>
    /// Scripted (no-LLM) trade NPC: asks for N of an item, takes them, rewards
    /// the player and fires events. Barks pick quest-state line first, then
    /// rotate through an idle pool without repeating the last two — Animal
    /// Crossing style. Wire an Interactable's OnInteract to <see cref="Talk"/>.
    /// Do NOT add NpcAuthoringLink here — that flips the prompt to LLM dialogue.
    /// </summary>
    public class ScriptedTradeNpc : MonoBehaviour
    {
        private enum TradeState { Waiting, Asked, Done }

        [Header("Request")]
        [SerializeField] private ItemDefinition _requiredItem;
        [SerializeField] private int _requiredCount = 3;

        [Header("Lines")]
        [Tooltip("First talk — states the request.")]
        [SerializeField, TextArea] private string _requestLine =
            "A maintenance unit? Been a while. My gate lever's seized — bring me 3 metal scrap and I'll crank it open.";
        [Tooltip("One-off reaction when the trade completes.")]
        [SerializeField, TextArea] private string _thanksLine =
            "Good scrap, this. There — gate's open. Top your cells off before you cross.";
        [Tooltip("Idle pool while the request is pending (rotates, no repeat within 2).")]
        [SerializeField, TextArea] private string[] _askedBarks =
        {
            "Metal scrap. Three pieces. The grey nodes past the rocks.",
            "No scrap, no crossing. Lever won't move itself.",
            "You hum when you walk. Old models did that too.",
            "Wind's been kinder lately. You fixing things has a smell to it.",
            "I'd help you look, but my knees rusted before you were compiled.",
            "The far side of the island. Grey, glinting. Can't miss it.",
        };
        [Tooltip("Idle pool after the trade (rotates, no repeat within 2).")]
        [SerializeField, TextArea] private string[] _doneBarks =
        {
            "Gate's open. Go on, before I get sentimental.",
            "Safe travels, little fixer.",
            "Tell the next island an old man says hello.",
            "You'll do fine out there. You listen. Rare, that.",
        };

        [Header("Reward")]
        [Tooltip("Fully recharges the robot battery on trade completion.")]
        [SerializeField] private bool _fullRecharge = true;

        [Header("Bark")]
        [SerializeField] private float _barkYOffset = 0.8f;
        [SerializeField] private int _barkSortingOrder = 10060;

        [Header("Events")]
        public UnityEvent onFirstTalk;
        public UnityEvent onTradeCompleted;

        private TradeState _state = TradeState.Waiting;
        private int _lastBark = -1;
        private int _prevBark = -1;

        /// <summary>Called by Interactable.OnInteract.</summary>
        public void Talk()
        {
            switch (_state)
            {
                case TradeState.Waiting:
                    _state = TradeState.Asked;
                    Bark(_requestLine);
                    onFirstTalk?.Invoke();
                    break;

                case TradeState.Asked:
                    if (TryCompleteTrade()) break;
                    Bark(NextFromPool(_askedBarks));
                    break;

                case TradeState.Done:
                    Bark(NextFromPool(_doneBarks));
                    break;
            }
        }

        private bool TryCompleteTrade()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || _requiredItem == null) return false;

            int have = 0;
            for (int i = 0; i < inv.SlotCount; i++)
            {
                var slot = inv.GetSlot(i);
                if (slot.item == _requiredItem) have += slot.count;
            }
            if (have < _requiredCount) return false;

            inv.TryConsume(_requiredItem, _requiredCount);
            _state = TradeState.Done;
            _lastBark = _prevBark = -1;

            Bark(_thanksLine);
            CodexAudio.PlaySecretUnlock();
            if (_fullRecharge && RobotBattery.Instance != null)
                RobotBattery.Instance.SetCharge(RobotBattery.Max);

            onTradeCompleted?.Invoke();
            return true;
        }

        private string NextFromPool(string[] pool)
        {
            if (pool == null || pool.Length == 0) return "...";
            if (pool.Length <= 2) return pool[Random.Range(0, pool.Length)];

            int pick;
            do { pick = Random.Range(0, pool.Length); }
            while (pick == _lastBark || pick == _prevBark);

            _prevBark = _lastBark;
            _lastBark = pick;
            return pool[pick];
        }

        private void Bark(string line)
        {
            BarkBubble.Spawn(transform.position + Vector3.up * _barkYOffset, line, _barkSortingOrder);
        }
    }
}
