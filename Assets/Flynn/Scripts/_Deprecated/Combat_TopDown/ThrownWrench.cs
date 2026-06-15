using UnityEngine;
using Flynn.Events;

/// <summary>
/// Projectile spawned by WrenchThrowController. Flies outbound to a target point,
/// hovers (returnDelay), then homes back to the thrower like a boomerang and is caught.
/// Spins a child visual for the boomerang look. On impact with a ResourceNode,
/// applies throw damage through the effectiveness system.
/// </summary>
public class ThrownWrench : MonoBehaviour
{
    private enum Phase { Outbound, Hover, Returning }

    private WrenchThrowController _owner;
    private WrenchConfig _config;
    private Vector3 _target;
    private Phase _phase;
    private float _hoverTimer;
    private Transform _spin;
    private Camera _cam;
    private float _roll;
    private bool _hasHitTarget;

    public void Launch(WrenchThrowController owner, Vector3 target, WrenchConfig config)
    {
        _owner   = owner;
        _target  = target;
        _config  = config;
        _phase   = Phase.Outbound;
        _spin    = transform.childCount > 0 ? transform.GetChild(0) : transform;
    }

    private void Update()
    {
        if (_config == null) return;

        switch (_phase)
        {
            case Phase.Outbound:
                MoveToward(_target, _config.throwSpeed);
                if (Reached(_target, 0.1f))
                {
                    _phase = Phase.Hover;
                    _hoverTimer = _config.returnDelay;
                }
                break;

            case Phase.Hover:
                _hoverTimer -= Time.deltaTime;
                if (_hoverTimer <= 0f) _phase = Phase.Returning;
                break;

            case Phase.Returning:
                Vector3 catchPoint = _owner != null ? _owner.CatchPoint : transform.position;
                MoveToward(catchPoint, _config.returnSpeed);
                if (Reached(catchPoint, _config.catchRadius))
                {
                    if (_owner != null) _owner.OnCaught();
                    Destroy(gameObject);
                }
                break;
        }
    }

    /// <summary>
    /// Detect collision with ResourceNodes during the outbound phase.
    /// Uses the effectiveness system to determine damage.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (_phase != Phase.Outbound || _hasHitTarget) return;

        ResourceNode node = collision.gameObject.GetComponentInParent<ResourceNode>();
        if (node == null) return;

        _hasHitTarget = true;

        ToolType tool = PlayerInventory.Instance != null
            ? ToolHitContext.FromItemType(PlayerInventory.Instance.ActiveItemType)
            : ToolType.Wrench;

        int damage = _config != null ? _config.heavySwingDamage : 2;
        Vector3 hitDir = (collision.transform.position - transform.position).normalized;

        var hit = new ToolHitContext(
            source: gameObject,
            toolType: tool,
            action: ToolActionType.Throw,
            damage: damage,
            hitPoint: collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position,
            hitDirection: hitDir,
            charge: 1f
        );

        node.TryApplyToolHit(hit);

        // Immediately start returning after impact
        _phase = Phase.Returning;
    }

    // Billboard the visual to the camera (like the player sprite) and spin it in the
    // screen plane so the boomerang reads as rotating regardless of view angle.
    private void LateUpdate()
    {
        if (_config == null || _spin == null) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        _roll += _config.spinSpeed * Time.deltaTime;
        _spin.rotation = _cam.transform.rotation * Quaternion.Euler(0f, 0f, _roll);
    }

    private void MoveToward(Vector3 p, float speed)
        => transform.position = Vector3.MoveTowards(transform.position, p, speed * Time.deltaTime);

    private bool Reached(Vector3 p, float radius)
        => (transform.position - p).sqrMagnitude <= radius * radius;
}
