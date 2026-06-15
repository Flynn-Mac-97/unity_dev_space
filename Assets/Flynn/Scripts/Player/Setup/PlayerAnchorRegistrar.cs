using UnityEngine;

/// <summary>
/// Registers this object's Transform into a <see cref="PlayerAnchor"/> SO so runtime-spawned
/// world items can home in on the player without scene-wide lookups. Put on the Player root and
/// assign the same PlayerAnchor asset used by the dropped-item prefabs.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnchorRegistrar : MonoBehaviour
{
    [SerializeField] private PlayerAnchor _anchor;

    private void OnEnable()
    {
        if (_anchor != null) _anchor.Set(transform);
    }

    private void OnDisable()
    {
        if (_anchor != null) _anchor.Clear(transform);
    }
}
