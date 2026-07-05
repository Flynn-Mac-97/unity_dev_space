using System;
using UnityEngine;


using Flynn.Core;
using Flynn.Resources;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Player
{
    /// <summary>
    /// Owns the runtime inventory state (copied from <see cref="InventoryData"/> on Awake — the SO
    /// itself is never mutated at runtime). Holds a flat array of <see cref="InventorySlot"/>s:
    /// slot 0 is the wrench slot, slots 1..N are general item slots that stack identical items up to
    /// <see cref="ItemDefinition.maxStack"/>. The slot count is data-driven and extendable.
    ///
    /// Accepts any item kind (resources, future tools, etc.). Currency items never enter a slot —
    /// they route to their <see cref="ItemDefinition.currencyTarget"/> at the collection site.
    /// Handles hotkey input (1-4) to switch the active slot; raises events for the HUD.
    ///
    /// Acts as the game's inventory manager: a singleton (<see cref="Instance"/>) so the HUD, pickup,
    /// drop, magnets and the wrench controllers all reach inventory through one access point and
    /// subscribe to its events — no FindObjectOfType, no per-system GetComponent. Single-player, so one
    /// instance is assumed. Runs early (negative execution order) so <see cref="Instance"/> exists
    /// before any subscriber's Awake/OnEnable.
    ///
    /// Pure manager — holds no scene-position state and never touches the character controller, so it
    /// lives on a dedicated manager GameObject (e.g. MANAGERS/INVENTORY), not on the Player. All users
    /// reach it through <see cref="Instance"/>; nothing requires it to share the Player's GameObject.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerInventory : MonoBehaviour
    {
        /// <summary>The active inventory manager. Set in Awake, cleared on destroy.</summary>
        public static PlayerInventory Instance { get; private set; }

        public const int WrenchSlot = 0;

        [SerializeField] private InventoryData _inventoryConfig;

        public event Action<int> OnSlotChanged;
        public event Action<int> OnActiveSlotChanged;

        private InventorySlot[] _runtimeSlots = Array.Empty<InventorySlot>();
        private int _activeSlot;

        // ── Public API ────────────────────────────────────────────────────────────

        public int SlotCount => _runtimeSlots.Length;

        public int ActiveSlotIndex
        {
            get => _activeSlot;
            private set
            {
                int clamped = Mathf.Clamp(value, 0, SlotCount - 1);
                if (_activeSlot == clamped) return;
                _activeSlot = clamped;
                OnActiveSlotChanged?.Invoke(_activeSlot);
            }
        }

        public InventorySlot ActiveSlot => GetSlot(_activeSlot);
        public ItemDefinition ActiveItem => ActiveSlot.item;

        /// <summary>ToolType of the currently selected slot, or None if it's empty.</summary>
        public ToolType ActiveItemType => ActiveItem != null ? ActiveItem.itemType : ToolType.None;

        /// <summary>True when no general slot is empty (can't accept a new item kind).</summary>
        public bool IsFull
        {
            get
            {
                for (int i = 1; i < _runtimeSlots.Length; i++)
                    if (_runtimeSlots[i].IsEmpty) return false;
                return true;
            }
        }

        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= _runtimeSlots.Length) return InventorySlot.Empty;
            return _runtimeSlots[index];
        }

        /// <summary>True if any slot currently holds an item of the given type.</summary>
        public bool HasItem(ToolType type)
        {
            for (int i = 0; i < _runtimeSlots.Length; i++)
                if (!_runtimeSlots[i].IsEmpty && _runtimeSlots[i].item.itemType == type) return true;
            return false;
        }

        /// <summary>
        /// True if at least one unit of <paramref name="item"/> can be accepted right now
        /// (a matching non-full stack, or an empty slot). Used by world-item magnets to decide
        /// whether to fly to the player or stay on the ground.
        /// </summary>
        public bool HasRoomFor(ItemDefinition item)
        {
            if (item == null || item.isCurrency) return false;

            if (item.itemType == ToolType.Wrench)
                return GetSlot(WrenchSlot).IsEmpty;

            for (int i = 1; i < _runtimeSlots.Length; i++)
            {
                if (_runtimeSlots[i].IsEmpty) return true;
                if (_runtimeSlots[i].item == item && !_runtimeSlots[i].IsFull) return true;
            }
            return false;
        }

        /// <summary>
        /// Add up to <paramref name="count"/> of <paramref name="item"/>, stacking into matching
        /// slots first then filling empty ones. Returns how many were actually added (0 = no room).
        /// Currency items are rejected here (they belong on the currency counter, not a slot).
        /// </summary>
        public int TryAddItem(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0 || item.isCurrency) return 0;

            if (item.itemType == ToolType.Wrench)
            {
                if (!GetSlot(WrenchSlot).IsEmpty) return 0;
                _runtimeSlots[WrenchSlot] = InventorySlot.Of(item, 1);
                OnSlotChanged?.Invoke(WrenchSlot);
                return 1;
            }

            int added = 0;

            // 1) Top up existing matching stacks.
            for (int i = 1; i < _runtimeSlots.Length && added < count; i++)
            {
                if (_runtimeSlots[i].item != item || _runtimeSlots[i].IsFull) continue;
                int take = Mathf.Min(_runtimeSlots[i].SpaceLeft, count - added);
                _runtimeSlots[i].count += take;
                added += take;
                OnSlotChanged?.Invoke(i);
            }

            // 2) Fill empty slots.
            for (int i = 1; i < _runtimeSlots.Length && added < count; i++)
            {
                if (!_runtimeSlots[i].IsEmpty) continue;
                int take = Mathf.Min(Mathf.Max(1, item.maxStack), count - added);
                _runtimeSlots[i] = InventorySlot.Of(item, take);
                added += take;
                OnSlotChanged?.Invoke(i);
            }

            return added;
        }

        /// <summary>Remove one unit from a slot; returns the item removed, or null if the slot was empty.</summary>
        public ItemDefinition RemoveOne(int index) => RemoveFromSlot(index, 1, out _);

        /// <summary>Remove the whole stack from a slot; returns the item and (via out) how many were removed.</summary>
        public ItemDefinition RemoveStack(int index, out int removed)
            => RemoveFromSlot(index, int.MaxValue, out removed);

        /// <summary>
        /// Consume up to <paramref name="count"/> units of a specific item across all slots.
        /// Returns how many were actually removed. Used by stations/crafting that spend a
        /// known resource (e.g. feeding the transmitter).
        /// </summary>
        public int TryConsume(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return 0;
            int removed = 0;
            for (int i = 0; i < _runtimeSlots.Length && removed < count; i++)
            {
                if (_runtimeSlots[i].IsEmpty || _runtimeSlots[i].item != item) continue;
                RemoveFromSlot(i, count - removed, out int got);
                removed += got;
            }
            return removed;
        }

        /// <summary>Remove up to <paramref name="count"/> units from a slot.</summary>
        public ItemDefinition RemoveFromSlot(int index, int count, out int removed)
        {
            removed = 0;
            if (index < 0 || index >= _runtimeSlots.Length || count <= 0) return null;

            InventorySlot slot = _runtimeSlots[index];
            if (slot.IsEmpty) return null;

            ItemDefinition item = slot.item;
            removed = Mathf.Min(count, slot.count);
            slot.count -= removed;
            _runtimeSlots[index] = slot.count <= 0 ? InventorySlot.Empty : slot;
            OnSlotChanged?.Invoke(index);
            return item;
        }

        // ── Unity messages ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[PlayerInventory] Second instance on '{name}'; overriding the previous Instance.", this);
            Instance = this;

            int count = _inventoryConfig != null ? _inventoryConfig.Slots : 4;
            _runtimeSlots = new InventorySlot[count];
            for (int i = 0; i < count; i++)
            {
                ItemDefinition def = _inventoryConfig != null ? _inventoryConfig.GetDefaultSlot(i) : null;
                _runtimeSlots[i] = def != null ? InventorySlot.Of(def, 1) : InventorySlot.Empty;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

}
