using UnityEngine;

/// <summary>
/// Data asset defining the effect a terrain zone applies. Each <see cref="TerrainEffectZone"/>
/// references one of these instead of having inline inspector fields. This makes terrain
/// effects data-driven and shareable across zones.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Terrain/Effect Data", fileName = "TerrainEffectData")]
public class TerrainEffectData : ScriptableObject
{
    [Tooltip("What kind of terrain this is (for events and identification).")]
    public TerrainEffectType type = TerrainEffectType.Wind;

    [Tooltip("The terrain state this zone applies to the player.")]
    public TerrainState state = new()
    {
        SpeedMultiplier = 1f,
        SteeringMultiplier = 1f,
        LowGrip = false,
        BlocksJump = false,
        BlocksRope = false,
        ExternalForce = Vector3.zero,
    };
}
