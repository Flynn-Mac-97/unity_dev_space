using UnityEngine;
using UnityEngine.Events;
using Flynn.Common;
using Flynn.Npc;
using Flynn.Player;

namespace Flynn.Pod
{
    /// <summary>
    /// The crashed pod's own power pool — the player's home base. Decays over
    /// time; the burner refills it via <see cref="AddPower"/>. While stabilised
    /// (above threshold) and the player stands close, the pod recharges the
    /// robot's battery — the "new day" moment of the loop.
    /// Deliberately NOT a TransmitterStation: that class publishes identity-less
    /// global events the HUD gauge and TransmitterGate already consume.
    /// </summary>
    public class PodPower : MonoBehaviour
    {
        [Header("Pool")]
        [SerializeField] private float _maxPower = 100f;
        [SerializeField] private float _startPower = 25f;
        [SerializeField] private float _decayPerSecond = 0.4f;
        [Tooltip("At or above this the pod counts as stabilised.")]
        [SerializeField] private float _stabiliseThreshold = 60f;

        [Header("Player Recharge")]
        [SerializeField] private PlayerAnchor _playerAnchor;
        [SerializeField] private float _chargeRadius = 2.5f;
        [Tooltip("Battery points per second while stabilised and player in radius.")]
        [SerializeField] private float _chargePerSecond = 20f;

        [Header("Visual")]
        [Tooltip("Renderer tinted dark when dead, lit when stabilised.")]
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _deadTint = new Color(0.35f, 0.38f, 0.45f, 1f);
        [SerializeField] private Color _litTint = Color.white;

        [Header("Events")]
        public UnityEvent onStabilised;
        public UnityEvent onDestabilised;
        public UnityEvent onDepleted;

        private float _power;
        private bool _stabilised;
        private float _chargeAccum;
        private bool _fullChimePlayed;

        public float Percent => _maxPower > 0f ? _power / _maxPower : 0f;
        public bool IsStabilised => _stabilised;

        private void Awake()
        {
            _power = Mathf.Clamp(_startPower, 0f, _maxPower);
            _stabilised = _power >= _stabiliseThreshold;
            ApplyTint();
        }

        public void AddPower(float amount)
        {
            if (amount <= 0f) return;
            _power = Mathf.Min(_maxPower, _power + amount);
            RefreshState();
        }

        private void Update()
        {
            if (_power > 0f && _decayPerSecond > 0f)
            {
                _power = Mathf.Max(0f, _power - _decayPerSecond * Time.deltaTime);
                if (_power <= 0f) onDepleted?.Invoke();
                RefreshState();
            }

            TickPlayerRecharge();
        }

        private void TickPlayerRecharge()
        {
            if (!_stabilised) return;
            var battery = RobotBattery.Instance;
            if (battery == null) return;
            if (battery.IsFull) return;
            if (_playerAnchor == null || !_playerAnchor.HasPlayer) return;
            if (Vector2.Distance(_playerAnchor.Current.position, transform.position) > _chargeRadius) return;

            _fullChimePlayed = false;
            _chargeAccum += _chargePerSecond * Time.deltaTime;
            int whole = Mathf.FloorToInt(_chargeAccum);
            if (whole <= 0) return;

            _chargeAccum -= whole;
            battery.AddCharge(whole);
            if (battery.IsFull && !_fullChimePlayed)
            {
                _fullChimePlayed = true;
                CodexAudio.PlayCodexChime();
            }
        }

        private void RefreshState()
        {
            bool now = _power >= _stabiliseThreshold;
            if (now != _stabilised)
            {
                _stabilised = now;
                if (now) { CodexAudio.PlayTrustUp(); onStabilised?.Invoke(); }
                else onDestabilised?.Invoke();
            }
            ApplyTint();
        }

        private void ApplyTint()
        {
            if (_renderer != null)
                _renderer.color = Color.Lerp(_deadTint, _litTint, Percent);
        }
    }
}
