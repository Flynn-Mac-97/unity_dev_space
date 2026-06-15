using UnityEngine;
using Flynn.Events;

/// <summary>
/// Manages the hold-to-charge power buildup for swing and throw actions.
/// Controllers call <see cref="BeginCharge"/> on mouse-down and <see cref="ReleaseCharge"/>
/// on mouse-up. The manager drives the charge timer each frame, publishes events for
/// the HUD bar, and returns a <see cref="ChargeResult"/> with scaled damage and battery cost.
///
/// Battery cost and damage scale linearly between light and heavy values in
/// <see cref="PowerBuildupSettings"/>. A "perfect zone" window at the right side
/// of the bar gives a satisfying timing indicator.
/// </summary>
[DefaultExecutionOrder(-35)]
public class PowerBuildupManager : MonoBehaviour
{
    public static PowerBuildupManager Instance { get; private set; }

    [SerializeField] private PowerBuildupSettings _settings;

    private enum State { Idle, Charging }

    private State _state = State.Idle;
    private ActionType _currentType;
    private float _chargeTimer;
    private bool _peaked; // true once charge hits 1.0 (used for decay)

    // ── Public API ──────────────────────────────────────────────────────────

    public bool IsCharging => _state == State.Charging;
    public float ChargeNormalized => _state != State.Charging ? 0f : NormalizedCharge();
    public ActionType CurrentType => _currentType;

    /// <summary>Begin charging for the given action type.</summary>
    public void BeginCharge(ActionType type)
    {
        if (_state == State.Charging) return;

        // Check minimum battery cost
        var battery = RobotBattery.Instance;
        float minCost = type == ActionType.Swing
            ? (_settings != null ? _settings.lightSwingBatteryCost : 2f)
            : (_settings != null ? _settings.lightThrowBatteryCost : 5f);
        if (battery != null && !battery.CanSpend(minCost)) return;

        _state = State.Charging;
        _currentType = type;
        _chargeTimer = 0f;
        _peaked = false;

        Publish(new PowerBuildupStarted(type));
    }

    /// <summary>Release the charge and get the result.</summary>
    public ChargeResult ReleaseCharge()
    {
        if (_state != State.Charging) return default;

        float norm = NormalizedCharge();
        bool isPerfect = _settings != null
            && norm >= _settings.perfectZoneStart
            && norm <= _settings.perfectZoneEnd;

        int damage;
        float batteryCost;
        float animSpeed;

        if (_currentType == ActionType.Swing)
        {
            damage = _settings != null
                ? Mathf.RoundToInt(Mathf.Lerp(_settings.lightSwingDamage, _settings.heavySwingDamage, norm))
                : 1;
            batteryCost = _settings != null
                ? Mathf.Lerp(_settings.lightSwingBatteryCost, _settings.heavySwingBatteryCost, norm)
                : 2f;
            animSpeed = _settings != null
                ? Mathf.Lerp(_settings.lightSwingAnimSpeed, _settings.heavySwingAnimSpeed, norm)
                : 1f;
        }
        else
        {
            damage = _settings != null
                ? Mathf.RoundToInt(Mathf.Lerp(_settings.lightThrowDamage, _settings.heavyThrowDamage, norm))
                : 1;
            batteryCost = _settings != null
                ? Mathf.Lerp(_settings.lightThrowBatteryCost, _settings.heavyThrowBatteryCost, norm)
                : 5f;
            animSpeed = 1f;
        }

        var result = new ChargeResult(norm, isPerfect, damage, batteryCost, animSpeed, _currentType);

        // Spend battery
        var battery = RobotBattery.Instance;
        if (battery != null && batteryCost > 0f) battery.Spend(batteryCost);

        Publish(new PowerBuildupReleased(norm, isPerfect, damage, batteryCost, _currentType));

        _state = State.Idle;
        _chargeTimer = 0f;
        _peaked = false;

        return result;
    }

    /// <summary>Cancel without releasing (e.g. unequipped wrench mid-charge).</summary>
    public void CancelCharge()
    {
        if (_state != State.Charging) return;
        _state = State.Idle;
        _chargeTimer = 0f;
        _peaked = false;
        Publish(new PowerBuildupReleased(0f, false, 0, 0f, _currentType));
    }

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[PowerBuildupManager] Duplicate; destroying {name}.", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_state != State.Charging) return;

        float maxTime = _settings != null ? _settings.maxChargeTime : 2f;

        if (!_peaked)
        {
            _chargeTimer += Time.deltaTime;
            if (_chargeTimer >= maxTime)
            {
                _chargeTimer = maxTime;
                _peaked = true;
            }
        }
        else if (_settings != null && _settings.decayRate > 0f)
        {
            _chargeTimer -= _settings.decayRate * Time.deltaTime;
            if (_chargeTimer <= 0f) _chargeTimer = 0f;
        }

        float norm = NormalizedCharge();
        bool inPerfect = _settings != null
            && norm >= _settings.perfectZoneStart
            && norm <= _settings.perfectZoneEnd;

        Publish(new PowerBuildupChanged(norm, inPerfect));
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private float NormalizedCharge()
    {
        float maxTime = _settings != null ? _settings.maxChargeTime : 2f;
        return maxTime > 0f ? Mathf.Clamp01(_chargeTimer / maxTime) : 0f;
    }

    private static void Publish<T>(T evt) where T : struct
    {
        if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
    }
}

/// <summary>
/// Result of a released charge. Controllers use this to determine damage,
/// battery cost, animation speed, and throw distance scaling.
/// </summary>
public struct ChargeResult
{
    public readonly float Normalized;
    public readonly bool IsPerfect;
    public readonly int Damage;
    public readonly float BatteryCost;
    public readonly float AnimSpeed;
    public readonly ActionType Type;

    public ChargeResult(float normalized, bool isPerfect, int damage, float batteryCost, float animSpeed, ActionType type)
    {
        Normalized = normalized;
        IsPerfect = isPerfect;
        Damage = damage;
        BatteryCost = batteryCost;
        AnimSpeed = animSpeed;
        Type = type;
    }
}
