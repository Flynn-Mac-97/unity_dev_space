using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lives on the Player. Aggregates all active <see cref="TerrainEffectZone2D"/> effects
/// into a single <see cref="CurrentState"/> that <see cref="PlayerController2D"/> reads
/// each physics tick. Zones register/unregister their effects on trigger enter/exit.
/// </summary>
public class TerrainStateAggregator2D : MonoBehaviour
{
    private readonly List<TerrainState2D> _activeStates = new();

    /// <summary>The combined terrain effect from all zones the player is currently inside.</summary>
    public TerrainState2D CurrentState { get; private set; } = TerrainState2D.Default;

    public void Register(TerrainState2D state)
    {
        _activeStates.Add(state);
        Recompute();
    }

    public void Unregister(TerrainState2D state)
    {
        _activeStates.Remove(state);
        Recompute();
    }

    private void Recompute()
    {
        var result = TerrainState2D.Default;
        foreach (var s in _activeStates)
            result = result.Compose(s);
        CurrentState = result;
    }
}
