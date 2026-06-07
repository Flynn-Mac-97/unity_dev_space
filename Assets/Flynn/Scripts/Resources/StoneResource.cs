using UnityEngine;

/// <summary>
/// First concrete resource on the event base. Subscribes to the <see cref="ResourceManager"/>'s
/// hit event on startup and logs every hit — nothing more. No damage, no targeting, no link to
/// <see cref="ResourceNode"/> yet; this only proves the player → manager → resource path.
/// </summary>
public class StoneResource : MonoBehaviour
{
    private void OnEnable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceHit += HandleHit;
        else
            Debug.LogWarning($"[StoneResource] No ResourceManager in scene; '{name}' won't receive hits.", this);
    }

    private void OnDisable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceHit -= HandleHit;
    }

    private void HandleHit(PlayerHitInfo info)
    {
        Debug.Log($"[StoneResource] '{name}' heard a player hit @ {info.Point} (power {info.Power}).", this);
    }
}
