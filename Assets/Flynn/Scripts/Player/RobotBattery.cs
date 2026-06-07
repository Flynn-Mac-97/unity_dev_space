using UnityEngine;

/// <summary>
/// Stub robot energy system. Owns a 0–100 <see cref="IntVariable"/> battery and ticks it down over
/// time; exposes charge/drain API. The HUD reads the battery purely through
/// <see cref="IntVariable.OnChanged"/> — same hub pattern as the other managers (this writes the
/// value, listeners react). Replace the passive-drain placeholder with real costs (actions, tools,
/// time-of-day) when the energy economy is designed.
///
/// One per scene; lives on a manager GameObject (e.g. MANAGERS/BATTERY).
/// </summary>
[DefaultExecutionOrder(-40)]
public class RobotBattery : MonoBehaviour
{
    public const int Min = 0;
    public const int Max = 100;

    public static RobotBattery Instance { get; private set; }

    [Tooltip("The shared 0–100 battery value the HUD displays. Assign the same asset on PlayerHudPanel.")]
    [SerializeField] private IntVariable _battery;

    [SerializeField, Range(Min, Max)] private int _startCharge = 100;

    [Tooltip("Passive drain in battery points per second. 0 = no auto-drain (placeholder economy).")]
    [SerializeField] private float _drainPerSecond = 1f;

    [SerializeField] private bool _autoDrain = true;

    private float _accum;

    // ── API ──────────────────────────────────────────────────────────────────

    public int Charge => _battery != null ? _battery.Value : 0;
    public bool IsEmpty => Charge <= Min;
    public bool IsFull  => Charge >= Max;

    /// <summary>Add charge (negative to drain). Clamped to 0–100; notifies via the IntVariable.</summary>
    public void AddCharge(int amount) => SetCharge(Charge + amount);

    /// <summary>Drain charge by a positive amount.</summary>
    public void Drain(int amount) => SetCharge(Charge - Mathf.Abs(amount));

    public void SetCharge(int value)
    {
        if (_battery == null) return;
        _battery.Value = Mathf.Clamp(value, Min, Max);
    }

    public void SetAutoDrain(bool on) => _autoDrain = on;

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
        if (!_autoDrain || _drainPerSecond <= 0f || _battery == null || IsEmpty) return;

        _accum += _drainPerSecond * Time.deltaTime;
        if (_accum < 1f) return;

        int whole = Mathf.FloorToInt(_accum);
        _accum -= whole;
        Drain(whole);
    }
}
