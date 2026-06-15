using UnityEngine;

/// <summary>
/// 2D trigger volume that applies a terrain effect to bodies inside it.
/// Supports Ice (low grip / slide), Wind (directional force), Mud (slow + no jump),
/// and Water (slow). Uses the <see cref="TerrainStateAggregator2D"/> pattern.
///
/// Wind is blocked by objects with the <see cref="WindResistant"/> component
/// (e.g. pushable crates). A raycast check determines if the player is in the
/// wind shadow of such an object.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TerrainEffectZone2D : MonoBehaviour
{
    [SerializeField] private TerrainEffectType type = TerrainEffectType.Wind;

    [Header("Wind")]
    [Tooltip("2D force applied to the player each physics step while inside and not shielded.")]
    [SerializeField] private Vector2 windForce = new Vector2(3f, 0f);

    [Header("Water / Mud")]
    [Tooltip("Speed multiplier while inside (lower = slower).")]
    [SerializeField, Range(0.05f, 1f)] private float speedMultiplier = 0.4f;

    [Header("Ice")]
    [Tooltip("Deceleration retention on ice (0 = infinite slide, 1 = normal grip).")]
    [SerializeField, Range(0.01f, 1f)] private float decelerationRetention = 0.1f;

    [Header("Wind Shadow")]
    [Tooltip("Max distance to check for WindResistant blockers.")]
    [SerializeField] private float _windShadowCheckDist = 20f;

    private Collider2D _zoneCollider;
    private TerrainState2D _registeredState;
    private bool _isRegistered;

    /// <summary>The terrain state this zone contributes to the aggregator.</summary>
    public TerrainState2D EffectState => type switch
    {
        TerrainEffectType.Wind => new TerrainState2D
        {
            ExternalForce = windForce,
        },
        TerrainEffectType.Water => new TerrainState2D
        {
            SpeedMultiplier = speedMultiplier,
        },
        TerrainEffectType.Mud => new TerrainState2D
        {
            SpeedMultiplier = speedMultiplier,
            BlocksJump = true,
        },
        TerrainEffectType.Ice => new TerrainState2D
        {
            DecelerationRetention = decelerationRetention,
            LowGrip = true,
        },
        _ => TerrainState2D.Default,
    };

    public TerrainEffectType ZoneType => type;
    public Vector2 WindForce => windForce;

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    private void Awake()
    {
        _zoneCollider = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody == null) return;
        if (other.GetComponentInParent<WindResistant>() != null) return;

        var aggregator = other.attachedRigidbody.GetComponent<TerrainStateAggregator2D>();
        if (aggregator == null) return;

        _registeredState = EffectState;
        aggregator.Register(_registeredState);
        _isRegistered = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.attachedRigidbody == null) return;
        if (other.GetComponentInParent<WindResistant>() != null) return;

        var aggregator = other.attachedRigidbody.GetComponent<TerrainStateAggregator2D>();
        if (aggregator == null || !_isRegistered) return;

        aggregator.Unregister(_registeredState);
        _isRegistered = false;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (type != TerrainEffectType.Wind) return;
        if (other.attachedRigidbody == null) return;
        if (other.GetComponentInParent<WindResistant>() != null) return;

        // Check if player is shielded by a WindResistant object
        if (IsInWindShadow(other.attachedRigidbody.position))
        {
            // Remove wind force from aggregator while shielded
            var aggregator = other.attachedRigidbody.GetComponent<TerrainStateAggregator2D>();
            if (aggregator != null && _isRegistered)
            {
                aggregator.Unregister(_registeredState);
                _isRegistered = false;
            }
        }
        else
        {
            // Re-register wind force if not currently registered
            var aggregator = other.attachedRigidbody.GetComponent<TerrainStateAggregator2D>();
            if (aggregator != null && !_isRegistered)
            {
                _registeredState = EffectState;
                aggregator.Register(_registeredState);
                _isRegistered = true;
            }
        }
    }

    /// <summary>
    /// Check if a position is shielded from wind by a <see cref="WindResistant"/> object.
    /// Raycasts from the position in the upwind direction.
    /// </summary>
    private bool IsInWindShadow(Vector2 position)
    {
        if (windForce.sqrMagnitude < 0.001f) return false;

        Vector2 upwind = -windForce.normalized;
        float maxDist = _zoneCollider != null
            ? _zoneCollider.bounds.size.magnitude
            : _windShadowCheckDist;

        var hit = Physics2D.Raycast(position, upwind, maxDist);
        return hit.collider != null && hit.collider.GetComponentInParent<WindResistant>() != null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (type == TerrainEffectType.Wind && windForce.sqrMagnitude > 0.01f)
        {
            Debug.DrawRay(transform.position, windForce.normalized * 2f, Color.yellow);
            Debug.DrawRay(transform.position, -windForce.normalized * 2f,
                new Color(0.5f, 0.5f, 1f, 0.5f));
        }
    }

    private void OnDrawGizmos()
    {
        var col = type switch
        {
            TerrainEffectType.Ice => new Color(0.6f, 0.9f, 1f, 0.15f),
            TerrainEffectType.Wind => new Color(0.9f, 0.9f, 0.3f, 0.15f),
            TerrainEffectType.Mud => new Color(0.5f, 0.3f, 0.1f, 0.15f),
            TerrainEffectType.Water => new Color(0.2f, 0.4f, 0.9f, 0.15f),
            _ => Color.clear,
        };

        Gizmos.color = col;
        if (_zoneCollider != null || TryGetComponent(out _zoneCollider))
        {
            var bounds = _zoneCollider.bounds;
            Gizmos.DrawCube(bounds.center, bounds.size);
        }

        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f,
            $"[{type}]" + (type == TerrainEffectType.Wind
                ? $" Wind:{windForce.x:F1},{windForce.y:F1}"
                : ""));
    }
#endif
}
