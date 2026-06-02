using UnityEngine;

/// <summary>
/// Picks up the WorldItemPickup the player is aiming at when the pickup key is
/// pressed: grants its item to the inventory and removes it from the world.
/// Reads the hovered pickup from PlayerMouseAimer; writes to PlayerInventory.
/// </summary>
[RequireComponent(typeof(PlayerMouseAimer))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerPickupController : MonoBehaviour
{
    [SerializeField] private KeyCode _pickupKey = KeyCode.E;

    private PlayerMouseAimer _aimer;
    private PlayerInventory  _inventory;

    private void Awake()
    {
        _aimer     = GetComponent<PlayerMouseAimer>();
        _inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(_pickupKey)) return;

        WorldItemPickup pickup = _aimer.HoveredPickup;
        if (pickup == null || pickup.Item == null) return;

        if (_inventory.TryAddItem(pickup.Item))
            Destroy(pickup.gameObject);
    }
}
