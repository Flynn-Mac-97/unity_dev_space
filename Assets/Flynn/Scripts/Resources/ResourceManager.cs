using System;
using UnityEngine;

/// <summary>
/// In-scene hub for resource events. A singleton so any script can reach it without Inspector
/// wiring: publishers call <see cref="RaiseHit"/>, subscribers do <c>OnResourceHit += ...</c> on
/// startup. The player and resources only ever know this manager — never each other.
///
/// For now it relays a single event (player hit). Add more events here as the resource layer grows;
/// the wiring pattern stays the same.
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

    /// <summary>Raised whenever a player hit lands. Subscribe with += on startup; unsubscribe with -=.</summary>
    public event Action<PlayerHitInfo> OnResourceHit;

    /// <summary>Publisher entry point: fire the hit event for all subscribers.</summary>
    public void RaiseHit(in PlayerHitInfo info)
    {
        if (_debugLog) Debug.Log($"[ResourceManager] hit @ {info.Point} target={(info.Target ? info.Target.name : "none")}");
        OnResourceHit?.Invoke(info);
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
