using UnityEngine;

/// <summary>
/// Immutable terrain effect state applied to the player by one or more terrain zones.
/// Zones compose additively: speed/steering are multiplied, forces are summed, booleans OR'd.
/// The controller reads this state each physics tick instead of having zones directly
/// mutate its properties.
/// </summary>
[System.Serializable]
public struct TerrainState
{
    public float SpeedMultiplier;
    public float SteeringMultiplier;
    public bool LowGrip;
    public bool BlocksJump;
    public bool BlocksRope;
    public Vector3 ExternalForce;

    public static TerrainState Default => new()
    {
        SpeedMultiplier = 1f,
        SteeringMultiplier = 1f,
        LowGrip = false,
        BlocksJump = false,
        BlocksRope = false,
        ExternalForce = Vector3.zero,
    };

    /// <summary>
    /// Compose two terrain states. Strategy:
    ///   Speed/Steering = product (1×1=1 normal; 0.4×0.5=0.2 stacked slow)
    ///   ExternalForce = sum (two wind zones blow harder)
    ///   Booleans = OR (any zone blocking jump means jump is blocked)
    /// </summary>
    public TerrainState Compose(TerrainState other)
    {
        return new TerrainState
        {
            SpeedMultiplier = SpeedMultiplier * other.SpeedMultiplier,
            SteeringMultiplier = SteeringMultiplier * other.SteeringMultiplier,
            LowGrip = LowGrip || other.LowGrip,
            BlocksJump = BlocksJump || other.BlocksJump,
            BlocksRope = BlocksRope || other.BlocksRope,
            ExternalForce = ExternalForce + other.ExternalForce,
        };
    }
}
