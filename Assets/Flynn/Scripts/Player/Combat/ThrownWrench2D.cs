using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D boomerang wrench projectile. Flies to a target, hovers, then returns to the
/// thrower's catch point and is caught. Damages each <see cref="ResourceNode"/> it
/// overlaps once per flight via <see cref="ResourceNode.TryApplyToolHit"/>.
/// Spawned and owned by <see cref="WrenchThrowController"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ThrownWrench2D : MonoBehaviour
{
    private enum Phase { Out, Hover, Back }

    private WrenchThrowController _owner;
    private WrenchConfig _config;
    private int _damage;
    private float _charge;

    private Phase _phase;
    private Vector2 _target;
    private float _hoverTimer;
    private Rigidbody2D _rb;

    private readonly HashSet<ResourceNode> _hitThisFlight = new();

    public void Launch(WrenchThrowController owner, Vector2 target, WrenchConfig config, int damage, float charge)
    {
        _owner = owner;
        _target = target;
        _config = config;
        _damage = damage;
        _charge = charge;

        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _phase = Phase.Out;
    }

    private void Update()
    {
        float spin = _config != null ? _config.spinSpeed : 720f;
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (_config == null || _rb == null) return;

        switch (_phase)
        {
            case Phase.Out:
                MoveToward(_target, _config.throwSpeed);
                if (Vector2.Distance(_rb.position, _target) < 0.2f)
                {
                    _phase = Phase.Hover;
                    _hoverTimer = _config.returnDelay;
                }
                break;

            case Phase.Hover:
                _hoverTimer -= Time.fixedDeltaTime;
                if (_hoverTimer <= 0f) _phase = Phase.Back;
                break;

            case Phase.Back:
                Vector2 catchPoint = _owner != null ? (Vector2)_owner.CatchPoint : _rb.position;
                MoveToward(catchPoint, _config.returnSpeed);
                if (Vector2.Distance(_rb.position, catchPoint) < _config.catchRadius)
                {
                    if (_owner != null) _owner.OnCaught();
                    Destroy(gameObject);
                    return;
                }
                break;
        }

        DamageOverlap();
    }

    private void MoveToward(Vector2 to, float speed)
        => _rb.position = Vector2.MoveTowards(_rb.position, to, speed * Time.fixedDeltaTime);

    private void DamageOverlap()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(_rb.position, 0.4f);
        for (int i = 0; i < hits.Length; i++)
        {
            ResourceNode node = hits[i].GetComponentInParent<ResourceNode>();
            if (node == null || node.IsDepleted || _hitThisFlight.Contains(node)) continue;

            _hitThisFlight.Add(node);
            Vector2 dir = (_target - _rb.position).sqrMagnitude > 0.0001f
                ? (_target - _rb.position).normalized : Vector2.right;
            var ctx = new ToolHitContext(
                gameObject, ToolType.Wrench, ToolActionType.Throw, _damage, _rb.position, dir, _charge);
            node.TryApplyToolHit(ctx);
        }
    }
}
