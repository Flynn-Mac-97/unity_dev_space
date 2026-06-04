using UnityEngine;

/// <summary>The kind of terrain interaction a <see cref="TerrainEffectZone"/> applies.</summary>
public enum TerrainEffectType { Wind, Water, Mud, Ice }

/// <summary>
/// A trigger volume that applies one terrain effect to bodies inside it, composing with the
/// force-based <see cref="SolarpunkCharacterController"/>:
/// - Wind: AddForce each step (affects the player AND light pushable blocks).
/// - Water: lowers the player's target speed (SpeedMultiplier) — noticeable slow.
/// - Mud: lowers target speed AND blocks jumping while grounded on it.
/// - Ice: lowers steering authority (SteeringControl) so momentum carries and direction is
///   hard to change; paired with a frictionless PhysicMaterial on the ice surface. Never 0,
///   so the player can always work their way off the ice (no soft-lock).
/// Single-responsibility: one zone, one configured effect. No statics, no Find.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TerrainEffectZone : MonoBehaviour
{
    [SerializeField] private TerrainEffectType type = TerrainEffectType.Wind;

    [Header("Wind")]
    [Tooltip("World-space force applied to each Rigidbody inside, every physics step. Large values blow the player back far.")]
    [SerializeField] private Vector3 windForce = new Vector3(60f, 0f, 0f);

    [Header("Water / Mud")]
    [Tooltip("Target-speed multiplier applied to the player while inside (lower = slower).")]
    [Range(0f, 1f)]
    [SerializeField] private float speedMultiplier = 0.4f;

    [Header("Ice / Wind steering")]
    [Tooltip("Steering-force multiplier while inside. Ice low = slidey; Wind low = controller stops fighting the wind so it blows you. Kept > 0 so the player can always escape.")]
    [Range(0.02f, 1f)]
    [SerializeField] private float steeringControl = 0.15f;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;

        if (type == TerrainEffectType.Wind)
            rb.AddForce(windForce, ForceMode.Force); // pushes the player AND light blocks

        var pc = rb.GetComponent<SolarpunkCharacterController>();
        if (pc == null) return;

        switch (type)
        {
            case TerrainEffectType.Wind:
                pc.SteeringControl = steeringControl; // weaken self-correction so wind blows the player
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
                pc.LowGrip = true; // glide: no braking when idle, momentum carries
                break;
        }
    }
}
