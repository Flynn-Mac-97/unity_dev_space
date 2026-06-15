/// <summary>
/// Previously managed the ResourceNode.NearbyNodes proximity registry via trigger
/// colliders. Superseded by the raycast-based detection in PlayerMouseAimer.
/// This component is intentionally empty — it remains to avoid missing-script
/// warnings on any prefabs that still reference it. Safe to remove from prefabs.
/// </summary>
public class ResourceNodeProximityRelay : UnityEngine.MonoBehaviour { }
