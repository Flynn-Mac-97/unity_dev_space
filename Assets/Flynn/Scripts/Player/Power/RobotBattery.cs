using System;
using UnityEngine;
using Flynn.Events;


using Flynn.Core;
using Flynn.Common;
using Flynn.UI.Core;
using Flynn.UI.Screens.PlayerHud;

namespace Flynn.Player
{
    /// <summary>
    /// Robot energy system. Owns a 0–100 battery and drains it
    /// both passively (over time) and actively (from gameplay actions via <see cref="BatterySettings"/>).
    /// The HUD reads battery through <see cref="Charge"/> and <see cref="OnChargeChanged"/>.
    ///
    /// Supports a configurable <see cref="BatterySettings"/> SO for action costs and an
    /// <c>infiniteBatteryDebug</c> toggle for sandbox testing without battery interference.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class RobotBattery : MonoBehaviour
    {
        public const int Min = 0;
        public const int Max = 100;

        public static RobotBattery Instance { get; private set; }

        /// <summary>Raised whenever the charge changes. Argument is the new value.</summary>
        public event Action<int> OnChargeChanged;

        [Tooltip("Battery settings: action costs, passive drain, debug mode.")]
        [SerializeField] private BatterySettings _settings;

        [SerializeField, Range(Min, Max)] private int _startCharge = 100;

        [Tooltip("Passive drain in battery points per second. Overridden by BatterySettings if assigned.")]
        [SerializeField] private float _drainPerSecond = 1f;

        [SerializeField] private bool _autoDrain = true;

        private int _charge;
        private float _accum;

        // ── API ──────────────────────────────────────────────────────────────────

        public int Charge => _charge;
        public bool IsEmpty => _charge <= Min;
        public bool IsFull  => _charge >= Max;

        /// <summary>Whether infinite battery debug mode is active.</summary>
        public bool InfiniteDebug => _settings != null && _settings.infiniteBatteryDebug;

        /// <summary>Access to battery settings for cost lookups (read-only).</summary>
        public BatterySettings Settings => _settings;

        /// <summary>
        /// Can the battery afford this cost? Returns true if infinite debug is on,
        /// or if there's enough charge (as a float, so partial charges are possible).
        /// </summary>
        public bool CanSpend(float cost)
        {
            if (_settings != null && _settings.infiniteBatteryDebug) return true;
            return Charge >= cost;
        }

        /// <summary>
        /// Spend a float amount of battery (for action costs). No-op in infinite debug mode.
        /// Publishes <see cref="BatteryChanged"/> and threshold events.
        /// </summary>
        public void Spend(float cost)
        {
            if (_settings != null && _settings.infiniteBatteryDebug) return;
            if (cost <= 0f) return;
            _accum -= cost;
            FlushAccum();
        }

        /// <summary>Drain over time (continuous actions like pulling/scanning).</summary>
        public void DrainOverTime(float costPerSecond)
        {
            if (_settings != null && _settings.infiniteBatteryDebug) return;
            _accum -= costPerSecond * Time.deltaTime;
            FlushAccum();
        }

        /// <summary>Add charge (negative to drain). Clamped to 0–100; notifies via the IntVariable.</summary>
        public void AddCharge(int amount) => SetCharge(Charge + amount);

        /// <summary>Drain charge by a positive amount.</summary>
        public void Drain(int amount) => SetCharge(Charge - Mathf.Abs(amount));

        public void SetCharge(int value)
        {
            int clamped = Mathf.Clamp(value, Min, Max);
            if (_charge == clamped) return;
            int prev = _charge;
            _charge = clamped;
            OnChargeChanged?.Invoke(_charge);

            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Publish(new BatteryChanged(_charge, Max));
                if (_charge <= Min && prev > Min)
                    GameEventBus.Instance.Publish(new BatteryEmpty());
                else if (_charge <= LowThreshold && prev > LowThreshold)
                    GameEventBus.Instance.Publish(new BatteryLow(_charge, (float)_charge / Max));
            }
        }

        public void SetAutoDrain(bool on) => _autoDrain = on;

        private int LowThreshold => _settings != null ? _settings.lowBatteryThreshold : 20;

        // ── Unity messages ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[RobotBattery] Duplicate; destroying {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            SetCharge(_startCharge);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_autoDrain || IsEmpty) return;
            float drainRate = _settings != null && _settings.enablePassiveDrain
                ? _settings.passiveDrainPerSecond
                : _drainPerSecond;

            if (drainRate <= 0f) return;

            _accum -= drainRate * Time.deltaTime;
            FlushAccum();
        }

        /// <summary>Flush accumulated fractional drain to the integer battery value.</summary>
        private void FlushAccum()
        {
            if (_accum >= 0f) return;
            int whole = Mathf.FloorToInt(-_accum);
            if (whole <= 0) return;
            _accum += whole;
            Drain(whole);
        }
    }

}
