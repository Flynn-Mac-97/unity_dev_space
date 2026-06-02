using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a world object as something the player can pick up. Requires a trigger
/// Collider for proximity. Mirrors ResourceNode's static-registry pattern: while
/// the player is inside the trigger, the pickup adds itself to NearbyPickups so
/// PlayerMouseAimer can discover it by aim without a direct reference.
/// PlayerPickupController grants <see cref="Item"/> to the inventory on pickup.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WorldItemPickup : MonoBehaviour
{
    /// <summary>All pickups currently within trigger range of the player.</summary>
    public static readonly HashSet<WorldItemPickup> NearbyPickups = new();

    [Tooltip("The item definition granted to the player's inventory on pickup.")]
    [SerializeField] private ItemDefinition _item;

    public ItemDefinition Item => _item;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<SolarpunkCharacterController>() != null)
            NearbyPickups.Add(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<SolarpunkCharacterController>() != null)
            NearbyPickups.Remove(this);
    }

    private void OnDisable() => NearbyPickups.Remove(this);
    private void OnDestroy() => NearbyPickups.Remove(this);
}
