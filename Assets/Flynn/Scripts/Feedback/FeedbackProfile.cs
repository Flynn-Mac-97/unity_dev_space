using UnityEngine;

/// <summary>
/// ScriptableObject that maps event types to visual feedback settings.
/// For MVP, defines the structure with string IDs. Prefab references will
/// be added when actual VFX assets are available.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Feedback Profile", fileName = "FeedbackProfile")]
public class FeedbackProfile : ScriptableObject
{
    [Header("Visual Effects")]
    [Tooltip("Prefab to instantiate when a resource is hit.")]
    public string hitEffectPrefabId;

    [Tooltip("Prefab to instantiate when a resource is depleted/destroyed.")]
    public string breakEffectPrefabId;

    [Tooltip("Prefab to instantiate at the tool impact point.")]
    public string impactEffectPrefabId;

    [Tooltip("Prefab for the tool swing arc/trail.")]
    public string swingEffectPrefabId;

    [Tooltip("Prefab for item pickup sparkles.")]
    public string pickupEffectPrefabId;

    [Tooltip("Prefab for grapple launch.")]
    public string grappleLaunchEffectPrefabId;

    [Header("Camera Shake")]
    [Tooltip("Intensity of camera shake on resource hit.")]
    public float cameraShakeIntensity = 0.1f;

    [Tooltip("Duration of camera shake on resource hit.")]
    public float cameraShakeDuration = 0.2f;

    [Header("Audio References")]
    [Tooltip("Sound ID for resource hit.")]
    public string soundHitId;

    [Tooltip("Sound ID for resource break.")]
    public string soundBreakId;

    [Tooltip("Sound ID for tool swing.")]
    public string soundSwingId;

    [Tooltip("Sound ID for pick up item.")]
    public string soundPickupId;
}
