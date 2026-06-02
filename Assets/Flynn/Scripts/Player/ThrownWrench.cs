using UnityEngine;

/// <summary>
/// Projectile spawned by WrenchThrowController. Flies outbound to a target point,
/// hovers (returnDelay), then homes back to the thrower like a boomerang and is caught.
/// Spins a child visual for the boomerang look. Placeholder square sprite.
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
