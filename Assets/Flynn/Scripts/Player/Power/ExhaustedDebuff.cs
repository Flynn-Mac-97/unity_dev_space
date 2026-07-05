using UnityEngine;
using Flynn.Core;
using Flynn.Events;
using Flynn.Npc;
using Flynn.Player.Combat;

namespace Flynn.Player
{
    /// <summary>
    /// Stardew-style exhaustion instead of a fail state: at 0 battery the robot
    /// crawls and can't swing; recharging (at the powered pod) restores both.
    /// Lives on the Player object next to RobotBattery.
    /// </summary>
    public class ExhaustedDebuff : MonoBehaviour
    {
        [Tooltip("Speed multiplier while exhausted.")]
        [SerializeField] private float _crawlMultiplier = 0.35f;
        [SerializeField] private string _warningLine = "Power critical. Return to the pod.";
        [SerializeField] private float _barkYOffset = 0.7f;

        private PlayerController2D _player;
        private WrenchController _wrench;
        private int _slowHandle = -1;
        private bool _exhausted;
        private bool _subscribed;

        private void Awake()
        {
            _player = GetComponent<PlayerController2D>();
            _wrench = GetComponent<WrenchController>();
        }

        // GameEventBus.Instance can be null during OnEnable off-MANAGERS —
        // retry from Start with a flag (known init race).
        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void OnDisable()
        {
            if (!_subscribed || GameEventBus.Instance == null) return;
            GameEventBus.Instance.Unsubscribe<BatteryEmpty>(OnBatteryEmpty);
            GameEventBus.Instance.Unsubscribe<BatteryChanged>(OnBatteryChanged);
            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed || GameEventBus.Instance == null) return;
            GameEventBus.Instance.Subscribe<BatteryEmpty>(OnBatteryEmpty);
            GameEventBus.Instance.Subscribe<BatteryChanged>(OnBatteryChanged);
            _subscribed = true;
        }

        private void OnBatteryEmpty(BatteryEmpty evt)
        {
            if (_exhausted) return;
            _exhausted = true;

            if (_player != null) _slowHandle = _player.AddSpeedModifier(_crawlMultiplier);
            if (_wrench != null) _wrench.enabled = false;
            BarkBubble.Spawn(transform.position + Vector3.up * _barkYOffset, _warningLine, 10060);
        }

        private void OnBatteryChanged(BatteryChanged evt)
        {
            if (!_exhausted || evt.Current <= 0) return;
            _exhausted = false;

            if (_player != null && _slowHandle >= 0)
            {
                _player.RemoveSpeedModifier(_slowHandle);
                _slowHandle = -1;
            }
            if (_wrench != null) _wrench.enabled = true;
        }
    }
}
