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

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (_doc == null || _inventory == null) return;

        var root = _doc.rootVisualElement;
        for (int i = 0; i < InventoryData.SlotCount; i++)
        {
            _slots[i] = root.Q<VisualElement>($"Slot_{i}");
            _icons[i] = root.Q<VisualElement>($"Icon_{i}");
        }

        _inventory.OnSlotChanged       += RefreshSlot;
        _inventory.OnActiveSlotChanged += RefreshActiveSlot;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (_inventory == null) return;
        _inventory.OnSlotChanged       -= RefreshSlot;
        _inventory.OnActiveSlotChanged -= RefreshActiveSlot;
    }

    // ── Private ───────────────────────────────────────────────────────────────

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

        var item = _inventory.GetSlot(slotIndex);
        if (item != null && item.icon != null)
        {
            _icons[slotIndex].style.backgroundImage = new StyleBackground(item.icon);
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
