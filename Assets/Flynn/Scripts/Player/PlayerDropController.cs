using UnityEngine;

/// <summary>
/// Drops items from the inventory back into the world. <b>G</b> drops one unit of the active slot;
/// the HUD calls <see cref="DropSlot"/> to drop a whole stack via drag-off-the-bar. Dropped items
/// spawn through <see cref="WorldItemSpawner"/> and get a short re-collect delay so an auto-collecting
/// resource doesn't instantly fly back into the inventory.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class PlayerDropController : MonoBehaviour
{
    [SerializeField] private KeyCode _dropKey = KeyCode.G;
    [SerializeField] private float _dropForward = 0.8f;
    [SerializeField] private float _dropHeight = 0.6f;
    [SerializeField] private float _popImpulse = 2f;
    [Tooltip("Seconds before a dropped auto-collect item may fly back to the player.")]
    [SerializeField] private float _reCollectDelay = 1.5f;

    private PlayerInventory _inventory;

    private void Awake() => _inventory = GetComponent<PlayerInventory>();

    private void Update()
    {
        if (Input.GetKeyDown(_dropKey))
            DropFromSlot(_inventory.ActiveSlotIndex, wholeStack: false);
    }

    /// <summary>Drop the entire stack in a slot (used by the hotbar drag-to-drop).</summary>
    public void DropSlot(int index) => DropFromSlot(index, wholeStack: true);

    private void DropFromSlot(int index, bool wholeStack)
    {
        InventorySlot slot = _inventory.GetSlot(index);
        if (slot.IsEmpty) return;

        ItemDefinition item = slot.item;
        // No world prefab → can't represent it on the ground; abort rather than vanish the item.
        if (item == null || item.worldPrefab == null)
        {
            Debug.LogWarning($"[PlayerDropController] '{(item != null ? item.displayName : "null")}' has no worldPrefab; drop ignored.", this);
            return;
        }

        int count;
        if (wholeStack) _inventory.RemoveStack(index, out count);
        else          { _inventory.RemoveOne(index); count = 1; }

        if (item == null || count <= 0) return;

        Vector3 pos = transform.position + Vector3.up * _dropHeight + transform.forward * _dropForward;
        WorldItem dropped = WorldItemSpawner.Spawn(item, count, pos, _popImpulse);
        if (dropped != null)
        {
            DroppedItemMagnet magnet = dropped.GetComponent<DroppedItemMagnet>();
            if (magnet != null) magnet.Suppress(_reCollectDelay);
        }
    }
}
