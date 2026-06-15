using UnityEngine;
using Flynn.Events;

/// <summary>
/// Picks up the key-pickup <see cref="WorldItem"/> the player is aiming at when the pickup key is
/// pressed: grants its item(s) to the inventory and removes it from the world. Only non-auto-collect
/// items (e.g. the wrench) need this — auto-collect drops fly to the player on their own.
/// Reads the hovered item from <see cref="PlayerMouseAimer"/>; writes to <see cref="PlayerInventory"/>.
/// </summary>
[RequireComponent(typeof(PlayerMouseAimer))]
public class PlayerPickupController : MonoBehaviour
{
    [SerializeField] private KeyCode _pickupKey = KeyCode.E;

    private PlayerMouseAimer _aimer;
    private PlayerCarryController _carry;

    private void Awake()
    {
        _aimer = GetComponent<PlayerMouseAimer>();
        _carry = GetComponent<PlayerCarryController>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(_pickupKey)) return;

        // Skip pickup while carrying an object
        if (_carry != null && _carry.IsCarrying) return;

        WorldItem item = _aimer.HoveredKeyPickup;
        if (item == null || item.Item == null) return;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;

        int added = inv.TryAddItem(item.Item, item.Count);
        if (added <= 0) return;

        if (GameEventBus.Instance != null)
            GameEventBus.Instance.Publish(new ItemPickedUp(item.Item, added));

        if (added >= item.Count) Destroy(item.gameObject);
        else                     item.Reduce(added);
    }
}
