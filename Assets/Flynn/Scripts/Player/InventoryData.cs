using UnityEngine;

/// <summary>
/// Pure config ScriptableObject — stores the default item loadout.
/// PlayerInventory copies this into a runtime array on Awake so the SO
/// is never mutated at runtime (no Editor-play-session bleed).
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Inventory Data", fileName = "InventoryData")]
public class InventoryData : ScriptableObject
{
    public const int SlotCount = 4;

    [Tooltip("Default items in each slot (0-3). Slot 0 is always the wrench.")]
    [SerializeField] private ItemDefinition[] _defaultSlots = new ItemDefinition[SlotCount];

    public ItemDefinition GetDefaultSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return null;
        if (_defaultSlots == null || index >= _defaultSlots.Length) return null;
        return _defaultSlots[index];
    }
}
