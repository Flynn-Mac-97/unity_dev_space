using UnityEngine;

/// <summary>
/// DEPRECATED — superseded by <see cref="WorldItem"/> (+ <see cref="DroppedItemMagnet"/> for
/// auto-collect, key-pickup via <see cref="PlayerMouseAimer.HoveredKeyPickup"/>). Kept only so the
/// retired UI-Toolkit HUDs still compile; new prefabs should use <see cref="WorldItem"/> instead.
///
/// Marks a world object as something the player can pick up. Detection is by mouse raycast in
/// <see cref="PlayerMouseAimer"/> — no registry, no trigger volume. The collider must be a
/// SOLID (non-trigger) collider on a layer in the aimer's pickup mask so the ray can hit it.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WorldItemPickup : MonoBehaviour
{
    [Tooltip("The item definition granted to the player's inventory on pickup.")]
    [SerializeField] private ItemDefinition _item;

    public ItemDefinition Item => _item;
}
