using UnityEngine;

/// <summary>
/// Right-click throw for the wrench. Tap = short throw; hold to charge for a longer
/// throw (distance scales min→max with charge). Spawns a ThrownWrench projectile that
/// flies to the target, hovers, then boomerangs back to the player and is caught.
/// Only one throw may be airborne at a time. Exposes IsCharging / ChargeNormalized /
/// IsAirborne for the aim reticle and the swing controller.
/// </summary>
[RequireComponent(typeof(PlayerMouseAimer))]
public class WrenchThrowController : MonoBehaviour
{
    [SerializeField] private WrenchConfig _config;
    [SerializeField] private GameObject _thrownWrenchPrefab;

    private PlayerMouseAimer _aimer;
    private IPlayerVisual _animDriver;
    private ThrownWrench _airborne;
    private float _chargeTimer;
    private bool _charging;

    public bool IsCharging => _charging;
    public bool IsAirborne => _airborne != null;
    public float ChargeNormalized => _config != null && _config.throwChargeTime > 0f
        ? Mathf.Clamp01(_chargeTimer / _config.throwChargeTime)
        : 0f;

    /// <summary>
    /// World-space hand position: the billboard visual centre, so the wrench
    /// appears to leave from and return to the sprite rather than the physics root.
    /// </summary>
    public Vector3 CatchPoint =>
        _animDriver != null ? _animDriver.VisualCenter : transform.position;

    private void Awake()
    {
        _aimer      = GetComponent<PlayerMouseAimer>();
        _animDriver = GetComponent<IPlayerVisual>();
    }

    private void Update()
    {
        bool usable = PlayerInventory.Instance != null
                      && PlayerInventory.Instance.ActiveItemType == ItemType.Wrench
                      && !IsAirborne;
        if (!usable)
        {
            _charging = false;
            _chargeTimer = 0f;
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            _charging = true;
            _chargeTimer = 0f;
        }
        else if (_charging && Input.GetMouseButton(1))
        {
            _chargeTimer += Time.deltaTime;
        }
        else if (_charging && Input.GetMouseButtonUp(1))
        {
            DoThrow();
            _charging = false;
            _chargeTimer = 0f;
        }
    }

    private void DoThrow()
    {
        if (_thrownWrenchPrefab == null || _config == null) return;

        // Use the billboard's visual centre as origin so distance/direction is
        // measured from where the sprite appears on screen, not from the feet.
        Vector3 origin = CatchPoint;

        // Direction on the XZ plane from the visual centre to the aim point.
        Vector3 toAim = _aimer.WorldAimPoint - origin;
        toAim.y = 0f;
        if (toAim.sqrMagnitude < 0.0001f) toAim = transform.forward;

        // Charge gates maximum reach. If the cursor is closer than that, the wrench
        // lands exactly on it; if farther, it stops at the charge-limited range.
        float maxDist = Mathf.Lerp(_config.minThrowDistance, _config.maxThrowDistance, ChargeNormalized);
        float dist = Mathf.Min(toAim.magnitude, maxDist);

        Vector3 target = origin + toAim.normalized * dist;
        // Restore the aim Y so the wrench lands at the actual world surface height.
        target.y = _aimer.WorldAimPoint.y;

        GameObject go = Instantiate(_thrownWrenchPrefab, CatchPoint, Quaternion.identity);
        _airborne = go.GetComponent<ThrownWrench>();
        if (_airborne != null) _airborne.Launch(this, target, _config);
        else Destroy(go);
    }

    /// <summary>Called by ThrownWrench when it has returned and been caught.</summary>
    public void OnCaught() => _airborne = null;
}
