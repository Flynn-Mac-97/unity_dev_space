using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Player
{
    /// <summary>
    /// Pure config ScriptableObject — stores the default item loadout.
    /// PlayerInventory copies this into a runtime array on Awake so the SO
    /// is never mutated at runtime (no Editor-play-session bleed).
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Inventory Data", fileName = "InventoryData")]
    public class InventoryData : ScriptableObject
    {
        [Tooltip("Number of general item slots after the wrench slot (slot 0). Start 3; extendable later.")]
        [Min(1)] public int resourceSlotCount = 3;

        [Tooltip("Default item in each slot. Index 0 is the wrench slot; the rest start empty.")]
        [SerializeField] private ItemDefinition[] _defaultSlots = new ItemDefinition[4];

        /// <summary>Total slots = the wrench slot (0) plus the general item slots.</summary>
        public int Slots => 1 + Mathf.Max(1, resourceSlotCount);

        public ItemDefinition GetDefaultSlot(int index)
        {
            if (_defaultSlots == null || index < 0 || index >= _defaultSlots.Length) return null;
            return _defaultSlots[index];
        }
    }

}
