using UnityEngine;

/// <summary>
/// Marker component: objects with this are ignored by <see cref="TerrainEffectZone"/>
/// wind force. Used on puzzle crates that should block wind but not be blown away
/// by it, while remaining pushable by the player through normal physics collision.
/// </summary>
[DisallowMultipleComponent]
public class WindResistant : MonoBehaviour { }