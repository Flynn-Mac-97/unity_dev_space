using System;
using UnityEngine;
using Flynn.Events;

/// <summary>
/// In-scene hub for resource events. A singleton so any script can reach it without Inspector
/// wiring: publishers call <see cref="RaiseHit"/>, subscribers do <c>OnResourceHit += ...</c> on
/// startup. The player and resources only ever know this manager — never each other.
///
/// Also owns the <see cref="ToolEffectivenessTable"/> that determines damage multipliers
/// based on tool type vs resource type. When a hit arrives, the manager looks up the
/// effectiveness and forwards a <see cref="ToolHitContext"/> (with scaled damage) to subscribers.
///
/// One per scene. Runs early (negative execution order) so <see cref="Instance"/> exists before any
/// subscriber's OnEnable runs.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [Tooltip("Log each raised hit. Off for production.")]
    [SerializeField] private bool _debugLog = true;

    [Tooltip("Maps tool×resource combinations to damage multipliers.")]
    [SerializeField] private ToolEffectivenessTable _effectivenessTable;

    /// <summary>Raised whenever a player hit lands. Subscribe with += on startup; unsubscribe with -=.</summary>
    public event Action<PlayerHitInfo> OnResourceHit;

    /// <summary>Publisher entry point: fire the hit event for all subscribers.</summary>
    public void RaiseHit(in PlayerHitInfo info)
    {
        if (_debugLog) Debug.Log($"[ResourceManager] hit @ {info.Point} target={(info.Target ? info.Target.name : "none")}");
        OnResourceHit?.Invoke(info);
    }

    /// <summary>
    /// Get the damage multiplier for a tool vs a resource type. Returns 0 for
    /// ineffective tools (no damage), partial for the wrench multitool, 1 for perfect match.
    /// </summary>
    public float GetEffectiveness(ToolType tool, ResourceType resource)
    {
        if (_effectivenessTable != null)
            return _effectivenessTable.GetMultiplier(tool, resource);
        // No table: default to full damage (backwards compatibility)
        return 1f;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ResourceManager] Duplicate; destroying {name}.", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
