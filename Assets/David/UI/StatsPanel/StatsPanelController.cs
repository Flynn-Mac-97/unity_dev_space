using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Stats popup toggled by the P key. Shows TRANSMITTER level, resource counts (wood/stone
/// from inventory slots matched by itemId), ECHO Shards and Battery charge.
/// Uses the same retry-bind + .hidden CSS pattern as DialogueManager and PauseMenuController.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class StatsPanelController : MonoBehaviour
{
    private const string OverlayName = "stats-overlay";
    private const string HiddenClass = "hidden";

    private const string TransmitterLevelName = "transmitter-level";
    private const string WoodCountName = "wood-count";
    private const string StoneCountName = "stone-count";
    private const string EchoCountName = "echo-count";
    private const string BatteryCountName = "battery-count";
    private const string ChargePctName = "charge-pct";

    [Header("TRANSMITTER")]
    [SerializeField, Range(1, 99)] private int _transmitterLevel = 1;

    [Header("Item IDs (match ItemDefinition.itemId)")]
    [SerializeField] private string _woodItemId = "wood";
    [SerializeField] private string _stoneItemId = "stone";

    [Header("Sources")]
    // [SerializeField] private PlayerInventory _inventory;
    // [SerializeField] private IntVariable _echoShards;
    // [SerializeField] private IntVariable _battery;

    private UIDocument _document;
    private VisualElement _overlay;
    private Label _transmitterLabel;
    private Label _woodLabel;
    private Label _stoneLabel;
    private Label _echoLabel;
    private Label _batteryLabel;
    private Label _chargeLabel;
    private bool _bound;
    private bool _isOpen;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        _bound = false;
        TryBind();
    }

    private void OnDisable()
    {
        // if (_inventory != null)
        //     _inventory.OnSlotChanged -= RefreshResources;
        // if (_echoShards != null)
        //     _echoShards.OnChanged -= SetEcho;
        // if (_battery != null)
        //     _battery.OnChanged -= SetBattery;
    }

    private void Update()
    {
        if (!_bound)
        {
            TryBind();
            return;
        }

        if (Input.GetKeyDown(KeyCode.P))
            Toggle();
    }

    private void TryBind()
    {
        if (_document == null) return;
        var root = _document.rootVisualElement;
        if (root == null) return;

        _overlay = root.Q<VisualElement>(OverlayName);
        if (_overlay == null) return;

        _transmitterLabel = root.Q<Label>(TransmitterLevelName);
        _woodLabel = root.Q<Label>(WoodCountName);
        _stoneLabel = root.Q<Label>(StoneCountName);
        _echoLabel = root.Q<Label>(EchoCountName);
        _batteryLabel = root.Q<Label>(BatteryCountName);
        _chargeLabel = root.Q<Label>(ChargePctName);

        if (_transmitterLabel == null || _woodLabel == null || _stoneLabel == null
            || _echoLabel == null || _batteryLabel == null || _chargeLabel == null) return;

        // if (_inventory == null) _inventory = PlayerInventory.Instance;
        // if (_inventory != null)
        //     _inventory.OnSlotChanged += RefreshResources;
        // if (_echoShards != null)
        //     _echoShards.OnChanged += SetEcho;
        // if (_battery != null)
        //     _battery.OnChanged += SetBattery;

        _bound = true;
        RefreshAll();
    }

    private void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    private void Open()
    {
        _isOpen = true;
        RefreshAll();
        _overlay.RemoveFromClassList(HiddenClass);
    }

    private void Close()
    {
        _isOpen = false;
        _overlay.AddToClassList(HiddenClass);
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    private void RefreshAll()
    {
        if (_transmitterLabel != null)
            _transmitterLabel.text = _transmitterLevel.ToString();
        RefreshResources(0);
        // if (_echoShards != null) SetEcho(_echoShards.Value);
        // if (_battery != null) SetBattery(_battery.Value);
    }

    private void RefreshResources(int _)
    {
        // if (_inventory == null) return;

        int wood = 0, stone = 0;
        // for (int i = 0; i < _inventory.SlotCount; i++)
        // {
        //     var slot = _inventory.GetSlot(i);
        //     if (slot.IsEmpty || slot.item == null) continue;

        //     if (slot.item.itemId == _woodItemId) wood += slot.count;
        //     else if (slot.item.itemId == _stoneItemId) stone += slot.count;
        // }

        if (_woodLabel != null) _woodLabel.text = wood.ToString();
        if (_stoneLabel != null) _stoneLabel.text = stone.ToString();
    }

    private void SetEcho(int value)
    {
        if (_echoLabel != null) _echoLabel.text = value.ToString();
    }

    private void SetBattery(int value)
    {
        int pct = Mathf.Clamp(value, 0, 100);
        if (_batteryLabel != null) _batteryLabel.text = pct.ToString();
        if (_chargeLabel != null) _chargeLabel.text = pct + "%";
    }

    /// <summary>Set the TRANSMITTER level at runtime. Clamped to 1–99.</summary>
    public void SetTransmitterLevel(int level)
    {
        _transmitterLevel = Mathf.Clamp(level, 1, 99);
        if (_bound && _transmitterLabel != null)
            _transmitterLabel.text = _transmitterLevel.ToString();
    }
}