using System;
using UnityEngine;

/// <summary>
/// Owns the runtime inventory state (copied from InventoryData SO on Awake —
/// the SO itself is never mutated at runtime). Handles hotkey input (1-4) to
/// switch the active slot. Other systems call SetSlot to add items in future
/// iterations.
/// </summary>
[RequireComponent(typeof(SolarpunkCharacterController))]
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private InventoryData _inventoryConfig;

    public event Action<int> OnSlotChanged;
    public event Action<int> OnActiveSlotChanged;

    private readonly ItemDefinition[] _runtimeSlots = new ItemDefinition[InventoryData.SlotCount];
    private int _activeSlot;

    // ── Public API ────────────────────────────────────────────────────────────

    public int ActiveSlotIndex
    {
        get => _activeSlot;
        private set
        {
            int clamped = Mathf.Clamp(value, 0, InventoryData.SlotCount - 1);
            if (_activeSlot == clamped) return;
            _activeSlot = clamped;
            OnActiveSlotChanged?.Invoke(_activeSlot);
        }
    }

    public ItemDefinition ActiveItem => GetSlot(_activeSlot);

    /// <summary>ItemType of the currently selected slot, or None if it's empty.</summary>
    public ItemType ActiveItemType => ActiveItem != null ? ActiveItem.itemType : ItemType.None;

    public ItemDefinition GetSlot(int index)
    {
        if (index < 0 || index >= InventoryData.SlotCount) return null;
        return _runtimeSlots[index];
    }

    /// <summary>
    /// Place an item into the given slot. Pass null to clear. Used by future
    /// pick-up logic.
    /// </summary>
    public void SetSlot(int index, ItemDefinition item)
    {
        if (index < 0 || index >= InventoryData.SlotCount) return;
        _runtimeSlots[index] = item;
        OnSlotChanged?.Invoke(index);
    }

    /// <summary>True if any slot currently holds an item of the given type.</summary>
    public bool HasItem(ItemType type)
    {
        for (int i = 0; i < InventoryData.SlotCount; i++)
            if (_runtimeSlots[i] != null && _runtimeSlots[i].itemType == type) return true;
        return false;
    }

    /// <summary>
    /// Adds an item to the inventory. The wrench multitool always lives in slot 0;
    /// everything else fills the first empty resource slot (1-3). Returns false when
    /// there's no room (or the wrench is already held). Used by pickup logic.
    /// </summary>
    public bool TryAddItem(ItemDefinition item)
    {
        if (item == null) return false;

        if (item.itemType == ItemType.Wrench)
        {
            if (GetSlot(0) != null) return false; // already carrying the wrench
            SetSlot(0, item);
            return true;
        }

        for (int i = 1; i < InventoryData.SlotCount; i++)
        {
            if (_runtimeSlots[i] == null)
            {
                SetSlot(i, item);
                return true;
            }
        }
        return false;
    }

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_inventoryConfig == null) return;
        for (int i = 0; i < InventoryData.SlotCount; i++)
            _runtimeSlots[i] = _inventoryConfig.GetDefaultSlot(i);
    }

    private void Update()
    {
        ReadHotkeys();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void ReadHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))      ActiveSlotIndex = 0;
        else if (Input.GetKeyDown(KeyCode.Alpha2)) ActiveSlotIndex = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha3)) ActiveSlotIndex = 2;
        else if (Input.GetKeyDown(KeyCode.Alpha4)) ActiveSlotIndex = 3;
    }
}
