using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The always-present player HUD as a single UI Toolkit panel: hotbar, echo-shard counter and the
/// robot battery gauge. A thin controller — the layout lives in PlayerHud.uxml/.uss, this only binds
/// elements and pushes values when the source data raises an event (no polling).
///
/// Driven by: <see cref="PlayerInventory"/> (hotbar), an <see cref="IntVariable"/> for echo shards
/// and an <see cref="IntVariable"/> (0–100) for the battery. Registered with <see cref="UIManager"/>
/// as <see cref="UIPanelId.Hud"/>; left visible the whole game.
/// </summary>
public class PlayerHudPanel : UIToolkitPanel
{
    [Header("Sources (echo/battery are asset refs; inventory falls back to Instance)")]
    [SerializeField] private PlayerInventory _inventory;
    [SerializeField] private IntVariable _echoShards;
    [SerializeField] private IntVariable _battery; // 0..100

    private readonly VisualElement[] _slots = new VisualElement[InventoryData.SlotCount];
    private readonly VisualElement[] _icons = new VisualElement[InventoryData.SlotCount];
    private readonly Label[] _counts = new Label[InventoryData.SlotCount];

    private Label _echoLabel;
    private VisualElement _batteryFill;
    private Label _batteryLabel;

    // ── Unity messages ────────────────────────────────────────────────────────

    protected override void OnEnable()
    {
        base.OnEnable();

        if (_inventory == null) _inventory = PlayerInventory.Instance;
        if (_inventory != null)
        {
            _inventory.OnSlotChanged       += RefreshSlot;
            _inventory.OnActiveSlotChanged += RefreshActiveSlot;
        }
        if (_echoShards != null) _echoShards.OnChanged += SetEcho;
        if (_battery    != null) _battery.OnChanged    += SetBattery;
    }

    private void OnDisable()
    {
        if (_inventory != null)
        {
            _inventory.OnSlotChanged       -= RefreshSlot;
            _inventory.OnActiveSlotChanged -= RefreshActiveSlot;
        }
        if (_echoShards != null) _echoShards.OnChanged -= SetEcho;
        if (_battery    != null) _battery.OnChanged    -= SetBattery;
    }

    // ── Binding ─────────────────────────────────────────────────────────────────

    protected override bool Bind(VisualElement root)
    {
        if (root.Q<VisualElement>("Slot_0") == null) return false; // tree not ready yet

        for (int i = 0; i < InventoryData.SlotCount; i++)
        {
            _slots[i]  = root.Q<VisualElement>($"Slot_{i}");
            _icons[i]  = root.Q<VisualElement>($"Icon_{i}");
            _counts[i] = root.Q<Label>($"Count_{i}");
        }
        _echoLabel    = root.Q<Label>("EchoCount");
        _batteryFill  = root.Q<VisualElement>("BatteryFill");
        _batteryLabel = root.Q<Label>("BatteryLabel");
        return true;
    }

    protected override void OnBound()
    {
        RefreshAll();
        if (_echoShards != null) SetEcho(_echoShards.Value);
        if (_battery    != null) SetBattery(_battery.Value);
    }

    // ── Refresh (event-driven) ──────────────────────────────────────────────────

    private void RefreshAll()
    {
        if (Root == null || _inventory == null) return;
        for (int i = 0; i < InventoryData.SlotCount && i < _inventory.SlotCount; i++) RefreshSlot(i);
        RefreshActiveSlot(_inventory.ActiveSlotIndex);
    }

    private void RefreshSlot(int i)
    {
        if (Root == null || i < 0 || i >= InventoryData.SlotCount) return;
        if (_icons[i] == null) return;

        InventorySlot slot = _inventory.GetSlot(i);
        bool hasItem = !slot.IsEmpty;

        if (hasItem && slot.item.icon != null)
        {
            _icons[i].style.backgroundImage = new StyleBackground(slot.item.icon);
            _icons[i].style.display = DisplayStyle.Flex;
        }
        else
        {
            _icons[i].style.backgroundImage = StyleKeyword.None;
            _icons[i].style.display = DisplayStyle.None;
        }

        if (_counts[i] != null)
        {
            bool show = hasItem && slot.count > 1;
            _counts[i].text = show ? slot.count.ToString() : string.Empty;
            _counts[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void RefreshActiveSlot(int active)
    {
        if (Root == null) return;
        for (int i = 0; i < InventoryData.SlotCount; i++)
        {
            if (_slots[i] == null) continue;
            if (i == active) _slots[i].AddToClassList("hotbar-slot--active");
            else             _slots[i].RemoveFromClassList("hotbar-slot--active");
        }
    }

    private void SetEcho(int value)
    {
        if (_echoLabel != null) _echoLabel.text = value.ToString();
    }

    private void SetBattery(int value)
    {
        int pct = Mathf.Clamp(value, 0, 100);
        if (_batteryFill != null) _batteryFill.style.width = new StyleLength(Length.Percent(pct));
        if (_batteryLabel != null) _batteryLabel.text = pct + "%";
    }
}
