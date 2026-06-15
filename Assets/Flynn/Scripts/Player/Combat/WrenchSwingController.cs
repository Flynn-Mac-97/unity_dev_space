using UnityEngine;
using Flynn.Events;

/// <summary>
/// Left-click wrench swing for the 2D view. Hold to charge power via
/// <see cref="PowerBuildupManager"/>, release to swing. The swing aims toward the
/// cursor; any <see cref="ResourceNode"/> inside the swing arc takes damage through
/// <see cref="ResourceNode.TryApplyToolHit"/> (tool-effectiveness applied there).
/// Requires the wrench as the active inventory item.
/// </summary>
public class WrenchSwingController : MonoBehaviour
{
    [Tooltip("Optional directional swipe arc visual.")]
    [SerializeField] private WrenchSwingArc _swingArc;
    [Tooltip("Reach from the player to the centre of the swing arc.")]
    [SerializeField] private float _swingRange = 1.4f;
    [Tooltip("Radius of the swing's hit area at that centre.")]
    [SerializeField] private float _hitRadius = 1.0f;
    [Tooltip("Cooldown between swings (seconds).")]
    [SerializeField] private float _cooldownTime = 0.15f;
    [Tooltip("Layers swing hits can land on.")]
    [SerializeField] private LayerMask _hitMask = ~0;
    [Tooltip("Camera for cursor aim. Falls back to Camera.main.")]
    [SerializeField] private Camera _cam;

    private PowerBuildupManager _buildup;
    private float _cooldown;
    private static readonly Plane PlayPlane = new Plane(Vector3.forward, Vector3.zero);

    public bool IsCharging => _buildup != null && _buildup.IsCharging && _buildup.CurrentType == ActionType.Swing;
    public float ChargeNormalized => _buildup != null ? _buildup.ChargeNormalized : 0f;

    private void Awake()
    {
        if (_cam == null) _cam = Camera.main;
    }

    private void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        if (_buildup == null) _buildup = PowerBuildupManager.Instance;

        bool usable = PlayerInventory.Instance != null
                      && PlayerInventory.Instance.ActiveItemType == ItemType.Wrench;
        if (!usable)
        {
            if (IsCharging) _buildup.CancelCharge();
            return;
        }

        if (Input.GetMouseButtonDown(0) && _cooldown <= 0f)
        {
            if (_buildup != null) _buildup.BeginCharge(ActionType.Swing);
        }
        else if (IsCharging && Input.GetMouseButtonUp(0))
        {
            DoSwing();
        }
    }

    private void DoSwing()
    {
        ChargeResult result = _buildup != null ? _buildup.ReleaseCharge() : default;
        int damage = Mathf.Max(1, result.Damage);

        Vector2 dir = AimDir();
        Vector2 hitCenter = (Vector2)transform.position + dir * _swingRange;

        if (_swingArc != null) _swingArc.Play(hitCenter);
        _cooldown = _cooldownTime;

        ToolType tool = PlayerInventory.Instance != null
            ? ToolHitContext.FromItemType(PlayerInventory.Instance.ActiveItemType)
            : ToolType.Wrench;

        // Announce the swing (audio / tutorial) whether or not it connects.
        Publish(new ToolSwingStarted(tool, hitCenter));

        // Damage the first resource node in the arc.
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitCenter, _hitRadius, _hitMask);
        for (int i = 0; i < hits.Length; i++)
        {
            ResourceNode node = hits[i].GetComponentInParent<ResourceNode>();
            if (node == null || node.IsDepleted) continue;

            var ctx = new ToolHitContext(
                gameObject, tool, ToolActionType.Swing, damage,
                hits[i].ClosestPoint(hitCenter), dir, result.Normalized);
            node.TryApplyToolHit(ctx);
            break; // one node per swing
        }
    }

    /// <summary>Direction from the player to the cursor on the play plane (+X fallback).</summary>
    private Vector2 AimDir()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (PlayPlane.Raycast(ray, out float d))
            {
                Vector2 to = (Vector2)ray.GetPoint(d) - (Vector2)transform.position;
                if (to.sqrMagnitude > 0.0001f) return to.normalized;
            }
        }
        return Vector2.right;
    }

    private static void Publish<T>(T evt) where T : struct
    {
        if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
    }
}
