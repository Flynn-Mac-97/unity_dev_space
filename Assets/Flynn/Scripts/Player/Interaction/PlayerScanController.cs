using UnityEngine;

/// <summary>
/// Scan interaction for the 2D view: hold the scan key while the cursor is over a
/// <see cref="ScanTarget"/> to scan it. Resolves the target by a cursor → z=0 plane
/// raycast + <c>Physics2D.OverlapPoint</c> (no aimer dependency). Drains battery while
/// scanning; on completion the ScanTarget grants its reveal item and publishes
/// <see cref="Flynn.Events.ScanRevealed"/> with its info/lore lines.
/// </summary>
public class PlayerScanController : MonoBehaviour
{
    [Tooltip("Key to hold for scanning.")]
    [SerializeField] private KeyCode _scanKey = KeyCode.F;
    [Tooltip("Max distance from the player to a scannable target.")]
    [SerializeField] private float _scanRange = 6f;
    [Tooltip("Layers scannable targets live on.")]
    [SerializeField] private LayerMask _scanMask = ~0;
    [Tooltip("Camera for the cursor raycast. Falls back to Camera.main.")]
    [SerializeField] private Camera _cam;

    private ScanTarget _activeTarget;
    private static readonly Plane PlayPlane = new Plane(Vector3.forward, Vector3.zero);

    private void Awake()
    {
        if (_cam == null) _cam = Camera.main;
    }

    private void Update()
    {
        ScanTarget hovered = ResolveTargetUnderCursor();

        // Start on key down.
        if (Input.GetKeyDown(_scanKey) && hovered != null && hovered.CanStart() && InRange(hovered))
        {
            _activeTarget = hovered;
            _activeTarget.Begin();
        }

        // Tick while held, target still under cursor and in range.
        if (Input.GetKey(_scanKey) && _activeTarget != null && _activeTarget.IsScanning)
        {
            if (hovered != _activeTarget || !InRange(_activeTarget)) { CancelActive(); return; }

            var battery = RobotBattery.Instance;
            float cost = _activeTarget.Config != null ? _activeTarget.Config.batteryCostPerSecond : 0f;
            if (cost > 0f && battery != null && !battery.CanSpend(cost * Time.deltaTime)) { CancelActive(); return; }

            _activeTarget.TickScan(Time.deltaTime);
            if (battery != null && cost > 0f) battery.DrainOverTime(cost);
        }

        // Stop on key up or when the scan ended.
        if (Input.GetKeyUp(_scanKey) || (_activeTarget != null && !_activeTarget.IsScanning))
            CancelActive();
    }

    private ScanTarget ResolveTargetUnderCursor()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return null;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (!PlayPlane.Raycast(ray, out float dist)) return null;

        Collider2D hit = Physics2D.OverlapPoint(ray.GetPoint(dist), _scanMask);
        return hit != null ? hit.GetComponentInParent<ScanTarget>() : null;
    }

    private bool InRange(ScanTarget target)
        => Vector2.Distance(transform.position, target.transform.position) <= _scanRange;

    private void CancelActive()
    {
        if (_activeTarget != null && _activeTarget.IsScanning) _activeTarget.CancelScan();
        _activeTarget = null;
    }
}
