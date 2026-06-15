using UnityEngine;
using Flynn.Events;

/// <summary>
/// Right-click wrench throw for the 2D view. Hold to charge via
/// <see cref="PowerBuildupManager"/>, release to throw toward the cursor. Spawns a
/// <see cref="ThrownWrench2D"/> boomerang that flies out, hovers, and returns to be
/// caught. Only one throw may be airborne at a time. Requires the active wrench.
/// </summary>
public class WrenchThrowController : MonoBehaviour
{
    [SerializeField] private WrenchConfig _config;
    [Tooltip("Prefab with a ThrownWrench2D component.")]
    [SerializeField] private GameObject _thrownWrenchPrefab;
    [Tooltip("Camera for cursor aim. Falls back to Camera.main.")]
    [SerializeField] private Camera _cam;

    private PowerBuildupManager _buildup;
    private ThrownWrench2D _airborne;
    private static readonly Plane PlayPlane = new Plane(Vector3.forward, Vector3.zero);

    public bool IsCharging => _buildup != null && _buildup.IsCharging && _buildup.CurrentType == ActionType.Throw;
    public float ChargeNormalized => _buildup != null ? _buildup.ChargeNormalized : 0f;
    public bool IsAirborne => _airborne != null;

    /// <summary>Where a thrown wrench is caught — the player's position.</summary>
    public Vector3 CatchPoint => transform.position;

    private void Awake()
    {
        if (_cam == null) _cam = Camera.main;
    }

    private void Update()
    {
        if (_buildup == null) _buildup = PowerBuildupManager.Instance;

        bool usable = PlayerInventory.Instance != null
                      && PlayerInventory.Instance.ActiveItemType == ItemType.Wrench
                      && !IsAirborne;
        if (!usable)
        {
            if (IsCharging) _buildup.CancelCharge();
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (_buildup != null) _buildup.BeginCharge(ActionType.Throw);
        }
        else if (IsCharging && Input.GetMouseButtonUp(1))
        {
            DoThrow();
        }
    }

    private void DoThrow()
    {
        if (_buildup == null || _thrownWrenchPrefab == null || _config == null) return;

        ChargeResult result = _buildup.ReleaseCharge();

        Vector2 origin = transform.position;
        Vector2 toAim = AimPoint() - origin;
        if (toAim.sqrMagnitude < 0.0001f) toAim = Vector2.right;

        float maxDist = Mathf.Lerp(_config.minThrowDistance, _config.maxThrowDistance, result.Normalized);
        float dist = Mathf.Min(toAim.magnitude, maxDist);
        Vector2 target = origin + toAim.normalized * dist;

        GameObject go = Instantiate(_thrownWrenchPrefab, origin, Quaternion.identity);
        _airborne = go.GetComponent<ThrownWrench2D>();
        if (_airborne != null) _airborne.Launch(this, target, _config, Mathf.Max(1, result.Damage), result.Normalized);
        else Destroy(go);

        if (GameEventBus.Instance != null)
            GameEventBus.Instance.Publish(new ToolThrowReleased(target, result.Normalized));
    }

    private Vector2 AimPoint()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (PlayPlane.Raycast(ray, out float d)) return ray.GetPoint(d);
        }
        return (Vector2)transform.position + Vector2.right;
    }

    /// <summary>Called by <see cref="ThrownWrench2D"/> when it returns and is caught.</summary>
    public void OnCaught() => _airborne = null;
}
