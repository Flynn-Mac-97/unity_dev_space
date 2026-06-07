using UnityEngine;

/// <summary>
/// Player startup script. Thin for now — owns the player's wiring to the <see cref="ResourceManager"/>:
/// it subscribes to resource events on startup (the place to add player-side reactions later) and is
/// the publisher that raises player hits via <see cref="EmitHit"/>. Combat is not wired in yet; a
/// debug key raises a test hit so the player → manager → resource pipe is verifiable in Play mode.
///
/// Expand this as the player gains more init responsibilities.
/// </summary>
public class PlayerInitializer : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Press to raise a test hit at the player's position (verifies the event pipe). None = disabled.")]
    [SerializeField] private KeyCode _debugHitKey = KeyCode.None;

    private void OnEnable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceHit += HandleResourceHit;
    }

    private void OnDisable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceHit -= HandleResourceHit;
    }

    private void Update()
    {
        if (_debugHitKey != KeyCode.None && Input.GetKeyDown(_debugHitKey))
            EmitHit(transform.position, null, 1);
    }

    /// <summary>Publisher: raise a player hit through the manager. Call this when a swing lands.</summary>
    public void EmitHit(Vector3 point, GameObject target = null, int power = 1)
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.RaiseHit(new PlayerHitInfo(point, target, power));
    }

    /// <summary>Player-side reaction to resource events. Empty hook for now.</summary>
    private void HandleResourceHit(PlayerHitInfo info) { }
}
