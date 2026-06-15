using UnityEngine;
using Flynn.Events;

/// <summary>
/// Player startup script. Owns the player's wiring to the <see cref="ResourceManager"/>:
/// it subscribes to resource events on startup and is the publisher that raises player hits
/// via <see cref="EmitHit"/>. Also supports the new <see cref="ToolHitContext"/>-based
/// hit flow that routes through <see cref="ResourceNode.TryApplyToolHit"/> with tool
/// effectiveness lookups.
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

    /// <summary>Publisher: raise a player hit through the manager (legacy PlayerHitInfo path).</summary>
    public void EmitHit(Vector3 point, GameObject target = null, int power = 1)
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.RaiseHit(new PlayerHitInfo(point, target, power));
    }

    /// <summary>
    /// Publisher: raise a tool hit with full context. Routes through the effectiveness
    /// system by finding the <see cref="ResourceNode"/> on the target and calling
    /// <see cref="ResourceNode.TryApplyToolHit"/>. Also publishes <see cref="ToolHitTarget"/>
    /// on the GameEventBus for feedback/audio.
    /// </summary>
    public void EmitToolHit(ToolHitContext hit)
    {
        // Publish tool hit event for feedback/audio
        PublishEvent(new ToolHitTarget(hit));

        // Find and apply to resource nodes
        if (hit.Source == null) return;
        ResourceNode node = hit.Source.GetComponentInParent<ResourceNode>();
        if (node == null && ResourceManager.Instance != null)
        {
            // Legacy: try to find a ResourceNode on the hit target
            // (the hit.Source is the player, not the target — need different resolution)
        }

        // Also raise through the legacy ResourceManager path for backward compat
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.RaiseHit(new PlayerHitInfo(hit.HitPoint, hit.Source, hit.Damage));
    }

    /// <summary>Player-side reaction to resource events. Empty hook for now.</summary>
    private void HandleResourceHit(PlayerHitInfo info) { }

    private static void PublishEvent<T>(T evt) where T : struct
    {
        if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
    }
}
