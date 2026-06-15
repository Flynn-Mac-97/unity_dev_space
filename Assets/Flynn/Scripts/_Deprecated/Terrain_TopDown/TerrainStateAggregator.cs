using System.Collections.Generic;
using UnityEngine;
using Flynn.Events;

/// <summary>
/// Lives on the Player. Aggregates all active <see cref="TerrainEffectZone"/> effects
/// into a single <see cref="CurrentTerrainState"/> that the <see cref="SolarpunkCharacterController"/>
/// reads each physics tick. Zones register/unregister their effects on enter/exit.
///
/// Composition strategy:
///   Speed/Steering = product (1×1=1 normal; 0.4×0.5=0.2 stacked slow)
///   ExternalForce = sum (two wind zones blow harder)
///   Booleans = OR (any zone blocking jump means jump is blocked)
/// </summary>
public class TerrainStateAggregator : MonoBehaviour
{
    private readonly List<TerrainState> _activeStates = new();

    /// <summary>The combined terrain effect from all zones the player is currently inside.</summary>
    public TerrainState CurrentTerrainState { get; private set; } = TerrainState.Default;

    /// <summary>Register a zone's effect (called by TerrainEffectZone on OnTriggerEnter).</summary>
    public void Register(TerrainState state)
    {
        _activeStates.Add(state);
        Recompute();
    }

    /// <summary>Unregister a zone's effect (called by TerrainEffectZone on OnTriggerExit).
    /// Uses reference equality on the struct; zones should cache their state reference.</summary>
    public void Unregister(TerrainState state)
    {
        _activeStates.Remove(state);
        Recompute();
    }

    private void Recompute()
    {
        var result = TerrainState.Default;
        foreach (var s in _activeStates)
            result = result.Compose(s);

        CurrentTerrainState = result;

        if (GameEventBus.Instance != null)
            GameEventBus.Instance.Publish(new TerrainStateChanged(CurrentTerrainState));
    }
}
