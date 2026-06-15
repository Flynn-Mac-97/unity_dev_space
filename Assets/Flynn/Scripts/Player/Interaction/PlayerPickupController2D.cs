using UnityEngine;
using Flynn.Events;

/// <summary>
/// 2D equivalent of <see cref="PlayerPickupController"/>. Reads the hovered key-pickup
/// <see cref="WorldItem"/> from <see cref="PlayerMouseAimer2D"/> and grants its item(s)
/// to the inventory on pickup key press.
/// </summary>
[RequireComponent(typeof(PlayerMouseAimer2D))]
public class PlayerPickupController2D : MonoBehaviour
{
    [SerializeField] private KeyCode _pickupKey = KeyCode.E;

    private PlayerMouseAimer2D _aimer;

    private void Awake()
    {
        _aimer = GetComponent<PlayerMouseAimer2D>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(_pickupKey)) return;

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
