using UnityEngine;

/// <summary>
/// 2D terrain state — identical to TerrainState but with Vector2 ExternalForce
/// for pure XY-plane scenes.
/// </summary>
[System.Serializable]
public struct TerrainState2D
{
    public float SpeedMultiplier;
    public float DecelerationRetention; // 1 = normal, <1 = icy slide
    public bool LowGrip;
    public bool BlocksJump;
    public Vector2 ExternalForce;

    public static TerrainState2D Default => new()
    {
        SpeedMultiplier = 1f,
        DecelerationRetention = 1f,
        LowGrip = false,
        BlocksJump = false,
        ExternalForce = Vector2.zero,
    };

    /// <summary>
    /// Compose two terrain states. Speed/DecelRetention = product,
    /// ExternalForce = sum, Booleans = OR.
    /// </summary>
    public TerrainState2D Compose(TerrainState2D other)
    {
        return new TerrainState2D
        {
            SpeedMultiplier = SpeedMultiplier * other.SpeedMultiplier,
            DecelerationRetention = DecelerationRetention * other.DecelerationRetention,
            LowGrip = LowGrip || other.LowGrip,
            BlocksJump = BlocksJump || other.BlocksJump,
            ExternalForce = ExternalForce + other.ExternalForce,
        };
    }
}
