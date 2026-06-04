using UnityEngine;

/// <summary>
/// Picks up the key-pickup <see cref="WorldItem"/> the player is aiming at when the pickup key is
/// pressed: grants its item(s) to the inventory and removes it from the world. Only non-auto-collect
/// items (e.g. the wrench) need this — auto-collect drops fly to the player on their own.
/// Reads the hovered item from <see cref="PlayerMouseAimer"/>; writes to <see cref="PlayerInventory"/>.
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

        WorldItem item = _aimer.HoveredKeyPickup;
        if (item == null || item.Item == null) return;

        int added = _inventory.TryAddItem(item.Item, item.Count);
        if (added <= 0) return;

        if (added >= item.Count) Destroy(item.gameObject);
        else                     item.Reduce(added);
    }
}
