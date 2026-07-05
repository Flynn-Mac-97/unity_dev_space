using System;
using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Player
{
    /// <summary>
    /// One inventory slot: an item kind plus how many of it are stacked here.
    /// A slot holds a single item kind; the stack is capped by <see cref="ItemDefinition.maxStack"/>.
    /// Kept as a small value type so the inventory model stays flat and expandable.
    /// </summary>
    [Serializable]
    public struct InventorySlot
    {
        public ItemDefinition item;
        public int count;

        public bool IsEmpty => item == null || count <= 0;

        /// <summary>Stack cap for whatever item this slot holds (1 when empty).</summary>
        public int MaxStack => item != null ? Mathf.Max(1, item.maxStack) : 1;

        public bool IsFull => !IsEmpty && count >= MaxStack;

        /// <summary>How many more of this item the slot can still take (0 when empty or full).</summary>
        public int SpaceLeft => IsEmpty ? 0 : Mathf.Max(0, MaxStack - count);

        public static InventorySlot Empty => new InventorySlot { item = null, count = 0 };

        public static InventorySlot Of(ItemDefinition item, int count)
            => new InventorySlot { item = item, count = item != null ? Mathf.Max(0, count) : 0 };
    }

}
