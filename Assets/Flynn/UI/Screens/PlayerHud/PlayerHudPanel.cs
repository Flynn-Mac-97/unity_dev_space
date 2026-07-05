using UnityEngine;
using UnityEngine.UIElements;
using Flynn.Events;


using Flynn.Core;
using Flynn.Common;
using Flynn.Player;
using Flynn.UI.Core;

namespace Flynn.UI.Screens.PlayerHud
{
    /// <summary>
    /// The always-present player HUD: hotbar, echo-shard counter, battery gauge,
    /// and power buildup bar. A thin controller — the layout lives in
    /// PlayerHud.uxml/.uss; this only binds elements and pushes values when source
    /// data raises an event (no polling).
    ///
    /// Driven by: <see cref="PlayerInventory"/> (hotbar), <see cref="IntVariable"/>
    /// for echo shards, <see cref="RobotBattery"/> for battery, and <see cref="GameEventBus"/> for power buildup
    /// events. Registered with <see cref="UIManager"/> as <see cref="UIPanelId.Hud"/>.
    /// </summary>
    public class PlayerHudPanel : UIToolkitPanel
    {
        private const int HudSlotCount = 4;

        [Header("Sources (echo is asset ref; inventory/battery fall back to Instance)")]
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private IntVariable _echoShards;

        [Header("Power buildup perfect zone (must match PowerBuildupSettings)")]
        [SerializeField, Range(0f, 1f)] private float _perfectZoneStart = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _perfectZoneEnd = 0.90f;

        private readonly VisualElement[] _slots = new VisualElement[HudSlotCount];
        private readonly VisualElement[] _icons = new VisualElement[HudSlotCount];
        private readonly Label[] _counts = new Label[HudSlotCount];

        private Label _echoLabel;
        private VisualElement _batteryFill;
        private Label _batteryLabel;

        // Transmitter objective gauge
        private VisualElement _transmitterFill;
        private Label _transmitterLabel;
        private const float TransmitterLowPercent = 0.25f; // fill turns red below this

        // Power buildup elements
        private VisualElement _buildupRoot;
        private VisualElement _buildupFill;
        private VisualElement _buildupPerfect;
        private Label _buildupLabel;

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
            if (RobotBattery.Instance != null) RobotBattery.Instance.OnChargeChanged += SetBattery;

            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Subscribe<CurrencyCollected>(OnCurrencyCollected);
                GameEventBus.Instance.Subscribe<PowerBuildupStarted>(OnBuildupStarted);
                GameEventBus.Instance.Subscribe<PowerBuildupChanged>(OnBuildupChanged);
                GameEventBus.Instance.Subscribe<PowerBuildupReleased>(OnBuildupReleased);
                GameEventBus.Instance.Subscribe<TransmitterPowerChanged>(OnTransmitterPower);
                GameEventBus.Instance.Subscribe<TransmitterDepleted>(OnTransmitterDepleted);
            }
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnSlotChanged       -= RefreshSlot;
                _inventory.OnActiveSlotChanged -= RefreshActiveSlot;
            }
            if (_echoShards != null) _echoShards.OnChanged -= SetEcho;
            if (RobotBattery.Instance != null) RobotBattery.Instance.OnChargeChanged -= SetBattery;

            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Unsubscribe<CurrencyCollected>(OnCurrencyCollected);
                GameEventBus.Instance.Unsubscribe<PowerBuildupStarted>(OnBuildupStarted);
                GameEventBus.Instance.Unsubscribe<PowerBuildupChanged>(OnBuildupChanged);
                GameEventBus.Instance.Unsubscribe<PowerBuildupReleased>(OnBuildupReleased);
                GameEventBus.Instance.Unsubscribe<TransmitterPowerChanged>(OnTransmitterPower);
                GameEventBus.Instance.Unsubscribe<TransmitterDepleted>(OnTransmitterDepleted);
            }
        }

        // ── Binding ─────────────────────────────────────────────────────────────────

        protected override bool Bind(VisualElement root)
        {
            if (root.Q<VisualElement>("Slot_0") == null) return false; // tree not ready yet

            for (int i = 0; i < HudSlotCount; i++)
            {
                _slots[i]  = root.Q<VisualElement>($"Slot_{i}");
                _icons[i]  = root.Q<VisualElement>($"Icon_{i}");
                _counts[i] = root.Q<Label>($"Count_{i}");
            }
            _echoLabel    = root.Q<Label>("EchoCount");
            _batteryFill  = root.Q<VisualElement>("BatteryFill");
            _batteryLabel = root.Q<Label>("BatteryLabel");

            _transmitterFill  = root.Q<VisualElement>("TransmitterFill");
            _transmitterLabel = root.Q<Label>("TransmitterLabel");

            // Power buildup elements
            _buildupRoot    = root.Q<VisualElement>("PowerBuildup");
            _buildupFill    = root.Q<VisualElement>("PowerBuildupFill");
            _buildupPerfect = root.Q<VisualElement>("PowerBuildupPerfect");
            _buildupLabel   = root.Q<Label>("PowerBuildupLabel");

            return true;
        }

        protected override void OnBound()
        {
            RefreshAll();
            if (_echoShards != null) SetEcho(_echoShards.Value);
            if (RobotBattery.Instance != null) SetBattery(RobotBattery.Instance.Charge);

            // Position the perfect zone indicator
            PositionPerfectZone();
        }

        // ── Refresh (event-driven) ──────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (Root == null || _inventory == null) return;
            for (int i = 0; i < HudSlotCount && i < _inventory.SlotCount; i++) RefreshSlot(i);
            RefreshActiveSlot(_inventory.ActiveSlotIndex);
        }

        private void RefreshSlot(int i)
        {
            if (Root == null || i < 0 || i >= HudSlotCount) return;
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
            for (int i = 0; i < HudSlotCount; i++)
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

        private void OnCurrencyCollected(CurrencyCollected evt) { }

        // ── Transmitter objective gauge (event-driven) ──────────────────────────────

        private void OnTransmitterPower(TransmitterPowerChanged evt)
        {
            float pct = Mathf.Clamp01(evt.Percent);
            if (_transmitterFill != null)
            {
                _transmitterFill.style.width = new StyleLength(Length.Percent(pct * 100f));
                if (pct <= TransmitterLowPercent)
                    _transmitterFill.AddToClassList("transmitter-fill--low");
                else
                    _transmitterFill.RemoveFromClassList("transmitter-fill--low");
            }
            if (_transmitterLabel != null)
                _transmitterLabel.text = Mathf.RoundToInt(pct * 100f) + "%";
        }

        private void OnTransmitterDepleted(TransmitterDepleted evt)
        {
            if (_transmitterFill != null)
                _transmitterFill.AddToClassList("transmitter-fill--low");
            if (_transmitterLabel != null)
                _transmitterLabel.text = "OFFLINE";
        }

        // ── Power buildup bar ─────────────────────────────────────────────────────

        private void PositionPerfectZone()
        {
            if (_buildupPerfect == null) return;
            float startPct = _perfectZoneStart * 100f;
            float widthPct = (_perfectZoneEnd - _perfectZoneStart) * 100f;
            _buildupPerfect.style.left = new StyleLength(Length.Percent(startPct));
            _buildupPerfect.style.width = new StyleLength(Length.Percent(widthPct));
        }

        private void OnBuildupStarted(PowerBuildupStarted evt)
        {
            if (_buildupRoot == null) return;
            _buildupRoot.style.display = DisplayStyle.Flex;
            if (_buildupFill != null)
            {
                _buildupFill.style.width = new StyleLength(Length.Percent(0));
                _buildupFill.RemoveFromClassList("power-buildup-fill--perfect");
                _buildupFill.RemoveFromClassList("power-buildup-fill--flash");
            }
            if (_buildupLabel != null) _buildupLabel.text = "";
        }

        private void OnBuildupChanged(PowerBuildupChanged evt)
        {
            if (_buildupFill == null) return;
            float pct = Mathf.Clamp01(evt.Normalized) * 100f;
            _buildupFill.style.width = new StyleLength(Length.Percent(pct));

            if (evt.InPerfectZone)
                _buildupFill.AddToClassList("power-buildup-fill--perfect");
            else
                _buildupFill.RemoveFromClassList("power-buildup-fill--perfect");

            if (_buildupLabel != null)
                _buildupLabel.text = evt.InPerfectZone ? "PERFECT!" : "";
        }

        private void OnBuildupReleased(PowerBuildupReleased evt)
        {
            if (_buildupFill != null)
            {
                _buildupFill.AddToClassList("power-buildup-fill--flash");
                _buildupFill.RemoveFromClassList("power-buildup-fill--perfect");
            }

            if (_buildupLabel != null)
                _buildupLabel.text = evt.IsPerfect ? "PERFECT!" : "";

            // Hide the bar after a brief flash
            if (_buildupRoot != null)
                _buildupRoot.schedule.Execute(() =>
                {
                    if (_buildupRoot != null) _buildupRoot.style.display = DisplayStyle.None;
                    if (_buildupFill != null) _buildupFill.RemoveFromClassList("power-buildup-fill--flash");
                }).StartingIn(200);
        }
    }

}
