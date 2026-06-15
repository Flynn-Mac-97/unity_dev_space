using UnityEngine;
using Flynn.Events;

/// <summary>
/// Robot energy system. Owns a 0–100 <see cref="IntVariable"/> battery and drains it
/// both passively (over time) and actively (from gameplay actions via <see cref="BatterySettings"/>).
/// The HUD reads the battery purely through <see cref="IntVariable.OnChanged"/>.
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

    [Tooltip("The shared 0–100 battery value the HUD displays. Assign the same asset on PlayerHudPanel.")]
    [SerializeField] private IntVariable _battery;

    [Tooltip("Battery settings: action costs, passive drain, debug mode.")]
    [SerializeField] private BatterySettings _settings;

    [SerializeField, Range(Min, Max)] private int _startCharge = 100;

    [Tooltip("Passive drain in battery points per second. Overridden by BatterySettings if assigned.")]
    [SerializeField] private float _drainPerSecond = 1f;

    [SerializeField] private bool _autoDrain = true;

    private float _accum;

    // ── API ──────────────────────────────────────────────────────────────────

    public int Charge => _battery != null ? _battery.Value : 0;
    public bool IsEmpty => Charge <= Min;
    public bool IsFull  => Charge >= Max;

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
        if (_battery == null) return;
        int prev = Charge;
        _battery.Value = Mathf.Clamp(value, Min, Max);
        int current = Charge;

        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Publish(new BatteryChanged(current, Max));
            if (current <= Min && prev > Min)
                GameEventBus.Instance.Publish(new BatteryEmpty());
            else if (current <= LowThreshold && prev > LowThreshold)
                GameEventBus.Instance.Publish(new BatteryLow(current, (float)current / Max));
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
        if (!_autoDrain || _battery == null || IsEmpty) return;

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
