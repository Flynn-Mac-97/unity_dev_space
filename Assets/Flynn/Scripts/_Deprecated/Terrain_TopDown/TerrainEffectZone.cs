using UnityEngine;
using Flynn.Events;

/// <summary>The kind of terrain interaction a <see cref="TerrainEffectZone"/> applies.</summary>
public enum TerrainEffectType { Wind, Water, Mud, Ice }

/// <summary>
/// A trigger volume that applies one terrain effect to bodies inside it. Now supports
/// the <see cref="TerrainStateAggregator"/> pattern: zones register/unregister their
/// <see cref="TerrainState"/> with the aggregator on enter/exit, and the aggregator
/// composes all active effects into a single state the controller reads.
///
/// Also retains the direct <see cref="OnTriggerStay"/> path for wind AddForce and
/// backwards compatibility with the existing <see cref="SolarpunkCharacterController"/>
/// property-mutation pattern. The controller will be migrated to read from the
/// aggregator in a future pass.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TerrainEffectZone : MonoBehaviour
{
    [SerializeField] private TerrainEffectType type = TerrainEffectType.Wind;

    [Header("Wind")]
    [Tooltip("World-space force applied to each Rigidbody inside, every physics step.")]
    [SerializeField] private Vector3 windForce = new Vector3(60f, 0f, 0f);

    [Header("Water / Mud")]
    [Tooltip("Target-speed multiplier applied to the player while inside (lower = slower).")]
    [Range(0f, 1f)]
    [SerializeField] private float speedMultiplier = 0.4f;

    [Header("Ice / Wind steering")]
    [Tooltip("Steering-force multiplier while inside.")]
    [Range(0.02f, 1f)]
    [SerializeField] private float steeringControl = 0.15f;

    /// <summary>The terrain state this zone contributes to the aggregator.</summary>
    public TerrainState EffectState => type switch
    {
        TerrainEffectType.Wind => new TerrainState
        {
            SpeedMultiplier = speedMultiplier,
            SteeringMultiplier = steeringControl,
            ExternalForce = windForce,
        },
        TerrainEffectType.Water => new TerrainState
        {
            SpeedMultiplier = speedMultiplier,
            SteeringMultiplier = steeringControl,
        },
        TerrainEffectType.Mud => new TerrainState
        {
            SpeedMultiplier = speedMultiplier,
            SteeringMultiplier = steeringControl,
            BlocksJump = true,
            BlocksRope = true,
        },
        TerrainEffectType.Ice => new TerrainState
        {
            SteeringMultiplier = steeringControl,
            LowGrip = true,
        },
        _ => TerrainState.Default,
    };

    /// <summary>Cached state for registration identity (used for unregister matching).</summary>
    private TerrainState _registeredState;
    private bool _isRegistered;
    private Collider _zoneCollider;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void Awake()
    {
        _zoneCollider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody == null) return;

        // WindResistant objects don't register terrain effects (they block wind, not experience it)
        if (other.GetComponentInParent<WindResistant>() != null) return;

        var pc = other.attachedRigidbody.GetComponent<SolarpunkCharacterController>();
        if (pc == null) return;

        // Register with the aggregator
        var aggregator = other.attachedRigidbody.GetComponent<TerrainStateAggregator>();
        if (aggregator != null)
        {
            _registeredState = EffectState;
            aggregator.Register(_registeredState);
            _isRegistered = true;
        }

        PublishEvent(new TerrainEntered(type, transform.position));
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody == null) return;

        // WindResistant objects never registered, so skip unregister too
        if (other.GetComponentInParent<WindResistant>() != null) return;

        var pc = other.attachedRigidbody.GetComponent<SolarpunkCharacterController>();
        if (pc == null) return;

        // Unregister from the aggregator
        var aggregator = other.attachedRigidbody.GetComponent<TerrainStateAggregator>();
        if (aggregator != null && _isRegistered)
        {
            aggregator.Unregister(_registeredState);
            _isRegistered = false;
        }

        PublishEvent(new TerrainExited(type));
    }

    private void OnTriggerStay(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;

        bool isWindResistant = other.GetComponentInParent<WindResistant>() != null;
        bool shieldedFromWind = false;

        // Wind force applied directly (affects physics objects too, not just the player).
        // WindResistant objects (puzzle crates) are skipped — they block wind but aren't blown.
        // Non-resistant bodies are also skipped if a WindResistant object is shielding them
        // from the wind (standing behind a crate relative to wind direction).
        if (type == TerrainEffectType.Wind && !isWindResistant)
        {
            shieldedFromWind = IsInWindShadow(rb.position);
            if (!shieldedFromWind)
                rb.AddForce(windForce, ForceMode.Force);
        }

        // Legacy: also mutate the controller properties directly for backwards compat.
        // Once the controller fully reads from the aggregator, this can be removed.
        var pc = rb.GetComponent<SolarpunkCharacterController>();
        if (pc == null) return;

        switch (type)
        {
            case TerrainEffectType.Wind:
                if (!shieldedFromWind)
                    pc.SteeringControl = steeringControl;
                break;
            case TerrainEffectType.Water:
                pc.SpeedMultiplier = speedMultiplier;
                break;
            case TerrainEffectType.Mud:
                pc.SpeedMultiplier = speedMultiplier;
                if (pc.IsGrounded) pc.CanJump = false;
                break;
            case TerrainEffectType.Ice:
                pc.SteeringControl = steeringControl;
                pc.LowGrip = true;
                break;
        }
    }

    /// <summary>
    /// Check if a position is shielded from wind by a <see cref="WindResistant"/> object.
    /// Casts a ray from the position in the upwind direction; if a WindResistant collider
    /// is hit (i.e. a crate is between this position and where the wind comes from),
    /// the position is in the crate's wind shadow.
    /// </summary>
    private bool IsInWindShadow(Vector3 position)
    {
        if (windForce.sqrMagnitude < 0.001f) return false;

        Vector3 upwind = -windForce.normalized;

        // Raycast distance: use the zone's trigger collider bounds so we only
        // detect shields within this zone's extent.
        float maxDist = _zoneCollider != null
            ? _zoneCollider.bounds.size.magnitude
            : 20f;

        // Slight vertical offset to avoid skimming the ground
        Vector3 origin = position + Vector3.up * 0.3f;

        if (Physics.Raycast(origin, upwind, out RaycastHit hit, maxDist))
        {
            if (hit.collider.GetComponentInParent<WindResistant>() != null)
                return true;
        }

        return false;
    }

    private static void PublishEvent<T>(T evt) where T : struct
    {
        if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw wind direction arrow for wind zones
        if (type == TerrainEffectType.Wind && windForce.sqrMagnitude > 0.01f)
        {
            Debug.DrawRay(transform.position, windForce.normalized * 2f, Color.yellow);

            // Draw upwind shadow check direction
            Vector3 upwind = -windForce.normalized;
            Debug.DrawRay(transform.position, upwind * 2f, new Color(0.5f, 0.5f, 1f, 0.5f));
        }

        // Label the zone type
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
            $"[{type}]" + (type == TerrainEffectType.Wind ? $" Wind:{windForce.x:F0},{windForce.z:F0}" : ""));
    }
#endif
}
