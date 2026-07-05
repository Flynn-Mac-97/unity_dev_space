using UnityEngine;
using Flynn.Events;

using Flynn.Core;
using Flynn.Common;
using Flynn.Npc;
using Flynn.Player;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Transmitter
{
    /// <summary>
    /// The start-zone objective. Holds a power pool that DECAYS over time; the player
    /// FEEDS gathered resources (press the feed key while in range) to refill it. While
    /// powered above the venture threshold the way out is open; if it drains to zero the
    /// player is locked in until they recharge it.
    ///
    /// SINGLE CONTROL POINT for the power loop: owns the power value + decay + feeding,
    /// and broadcasts everything over the event bus
    /// (TransmitterPowerChanged / TransmitterFed / TransmitterVentureStateChanged /
    /// TransmitterDepleted). HUD, gate and NPCs just subscribe — nothing references back here.
    /// </summary>
    public class TransmitterStation : MonoBehaviour, IInteractionPromptProvider
    {
        [Header("Power")]
        [SerializeField] private float _maxPower = 100f;
        [SerializeField] private float _startPower = 40f;
        [Tooltip("Power lost per second.")]
        [SerializeField] private float _decayPerSecond = 0.5f;
        [Tooltip("At/above this power the venture gate opens.")]
        [SerializeField] private float _ventureThreshold = 60f;

        [Header("Feeding")]
        [Tooltip("Which resources fuel this transmitter and how much each restores.")]
        [SerializeField] private TransmitterFuelTable _fuel;
        [Tooltip("How close the player must be to feed.")]
        [SerializeField] private float _feedRadius = 2f;
        [Tooltip("Feed key. NOT E — E is talk/pickup; use a free key (default R).")]
        [SerializeField] private KeyCode _feedKey = KeyCode.R;
        [Tooltip("Units of fuel consumed per feed press.")]
        [SerializeField] private int _unitsPerFeed = 1;

        [Header("Player")]
        [Tooltip("SO handle to the player's transform. Assign the project's PlayerAnchor asset.")]
        [SerializeField] private PlayerAnchor _playerAnchor;

        // ── State other systems read ──────────────────────────────────────────
        public float Power { get; private set; }
        public float Percent => _maxPower > 0f ? Power / _maxPower : 0f;
        public bool IsPowered => Power >= _ventureThreshold;
        public bool IsDepleted => Power <= 0f;

        private bool _wasPowered;
        private bool _wasDepleted;

        private void Start()
        {
            Power = Mathf.Clamp(_startPower, 0f, _maxPower);
            _wasPowered = IsPowered;
            _wasDepleted = IsDepleted;
            PublishPower();
            // Sync the gate to the starting state immediately.
            Bus(new TransmitterVentureStateChanged(IsPowered));
        }

        private void Update()
        {
            // Passive decay.
            if (Power > 0f)
            {
                Power = Mathf.Max(0f, Power - _decayPerSecond * Time.deltaTime);
                PublishPower();
                CheckThresholds();
            }

            // Feed on key press within range.
            if (Input.GetKeyDown(_feedKey) && PlayerInRange())
                TryFeed();
        }

        /// <summary>Consume one accepted fuel item from the inventory and add its power.
        /// Public so an interaction prompt or NPC can also trigger a feed.</summary>
        public void TryFeed()
        {
            if (_fuel == null || PlayerInventory.Instance == null) return;

            var inv = PlayerInventory.Instance;
            for (int i = 0; i < inv.SlotCount; i++)
            {
                var slot = inv.GetSlot(i);
                if (slot.IsEmpty) continue;

                float per = _fuel.PowerFor(slot.item);
                if (per <= 0f) continue; // not fuel

                int consumed = inv.TryConsume(slot.item, _unitsPerFeed);
                if (consumed <= 0) continue;

                AddPower(per * consumed);
                Bus(new TransmitterFed(slot.item, per * consumed));
                return; // one fuel kind per press
            }
        }

        /// <summary>
        /// Surfaces the feed prompt to the hover system (<see cref="WorldInteractTagPresenter"/>):
        /// "[R]  Feed Transmitter" whenever the player is within feed range. Pressing the feed key
        /// is still handled in <see cref="Update"/> — this only makes the interaction discoverable.
        /// </summary>
        public bool TryGetPrompt(out InteractionPrompt prompt)
        {
            // When set up as an NPC, show the talk prompt instead of feed.
            if (GetComponent<NpcAuthoringLink>() != null)
            {
                prompt = new InteractionPrompt("Click", "Talk", transform);
                return true;
            }

            if (PlayerInRange())
            {
                prompt = new InteractionPrompt(_feedKey.ToString(), "Feed", transform, "Transmitter");
                return true;
            }
            prompt = default;
            return false;
        }

        public void AddPower(float amount)
        {
            if (amount == 0f) return;
            Power = Mathf.Clamp(Power + amount, 0f, _maxPower);
            PublishPower();
            CheckThresholds();
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private bool PlayerInRange()
        {
            if (_playerAnchor == null || !_playerAnchor.HasPlayer) return false;
            return Vector2.Distance(_playerAnchor.Current.position, transform.position) <= _feedRadius;
        }

        private void CheckThresholds()
        {
            bool powered = IsPowered;
            if (powered != _wasPowered)
            {
                _wasPowered = powered;
                Bus(new TransmitterVentureStateChanged(powered));
            }

            bool depleted = IsDepleted;
            if (depleted != _wasDepleted)
            {
                _wasDepleted = depleted;
                if (depleted) Bus(new TransmitterDepleted());
            }
        }

        private void PublishPower() => Bus(new TransmitterPowerChanged(Power, _maxPower));

        private static void Bus<T>(T evt) where T : struct
        {
            if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _feedRadius);
        }
#endif
    }
}
