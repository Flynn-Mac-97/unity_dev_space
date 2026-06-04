using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Binds PlayerInventory runtime state to the Hotbar.uxml visual tree.
/// Attach this component to the same GameObject as the UIDocument.
/// The UIDocument's Source Asset must be Hotbar.uxml.
///
/// Subscribes to PlayerInventory events in OnEnable / OnDisable so
/// the UI always reflects current inventory without polling.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HotbarController : MonoBehaviour
{
    [SerializeField] private PlayerInventory _inventory;

    private UIDocument _doc;
    private readonly VisualElement[] _slots = new VisualElement[InventoryData.SlotCount];
    private readonly VisualElement[] _icons = new VisualElement[InventoryData.SlotCount];
    private bool _bound;

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (_inventory == null) return;
        _inventory.OnSlotChanged       += RefreshSlot;
        _inventory.OnActiveSlotChanged += RefreshActiveSlot;
        _bound = false;
        TryBind();      // may fail this frame if UIDocument hasn't built its tree yet — Update retries.
    }

    private void OnDisable()
    {
        if (_inventory == null) return;
        _inventory.OnSlotChanged       -= RefreshSlot;
        _inventory.OnActiveSlotChanged -= RefreshActiveSlot;
    }

    private void Update()
    {
        if (!_bound) TryBind();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve the slot/icon elements once the UIDocument has actually built its visual tree.
    /// rootVisualElement (and its children) may not exist yet during OnEnable depending on
    /// component OnEnable order, so this is retried from Update until it succeeds.
    /// </summary>
    private void TryBind()
    {
        if (_doc == null || _inventory == null) return;
        var root = _doc.rootVisualElement;
        if (root == null) return;

        var slot0 = root.Q<VisualElement>("Slot_0");
        if (slot0 == null) return;      // tree not populated yet — try again next frame.

        for (int i = 0; i < InventoryData.SlotCount; i++)
        {
            _slots[i] = root.Q<VisualElement>($"Slot_{i}");
            _icons[i] = root.Q<VisualElement>($"Icon_{i}");
        }

        _bound = true;
        RefreshAll();
    }

    private void RefreshAll()
    {
        for (int i = 0; i < InventoryData.SlotCount; i++)
            RefreshSlot(i);
        RefreshActiveSlot(_inventory.ActiveSlotIndex);
    }

    private void RefreshSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= InventoryData.SlotCount) return;
        if (_icons[slotIndex] == null) return;

        InventorySlot slot = _inventory.GetSlot(slotIndex);
        if (!slot.IsEmpty && slot.item.icon != null)
        {
            _icons[slotIndex].style.backgroundImage = new StyleBackground(slot.item.icon);
            _icons[slotIndex].style.display         = DisplayStyle.Flex;
        }
        else
        {
            _icons[slotIndex].style.backgroundImage = StyleKeyword.None;
            _icons[slotIndex].style.display         = DisplayStyle.None;
        }
    }

    private void RefreshActiveSlot(int activeIndex)
    {
        for (int i = 0; i < InventoryData.SlotCount; i++)
        {
            if (_slots[i] == null) continue;
            if (i == activeIndex)
                _slots[i].AddToClassList("hotbar-slot--active");
            else
                _slots[i].RemoveFromClassList("hotbar-slot--active");
        }
    }
}
