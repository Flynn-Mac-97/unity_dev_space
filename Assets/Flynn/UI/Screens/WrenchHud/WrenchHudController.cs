using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Screen-space HUD for the wrench: a charge meter that fills while a swing/throw is building,
/// a hovered-resource health bar, and the pickup prompt. The aim reticle is retired — the cursor
/// is a hardware texture now (see CustomCursor), so the UXML "Cursor" element is hidden on enable.
/// Reads the wrench controllers (assigned in the Inspector) — no scene lookups. Attach to the same
/// GameObject as a UIDocument whose Source Asset is WrenchHud.uxml.
///
/// Also drives the generic pickup interaction prompt: when the player's mouse aims at a
/// nearby WorldItemPickup, a small panel appears above it showing the item name and
/// "[E] Pick Up" hint.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WrenchHudController : MonoBehaviour
{
    [SerializeField] private WrenchSwingController _swing;
    [SerializeField] private WrenchThrowController _throw;
    [SerializeField] private WrenchConfig _config;
    [SerializeField] private PlayerMouseAimer _aimer;

    [Tooltip("Vertical offset in panel pixels to push the pickup prompt above the item.")]
    [SerializeField] private float _pickupPromptOffsetY = 56f;

    private const float TrackInner = 58f; // 60px track minus the 1px borders

    private UIDocument _doc;
    private VisualElement _cursor;
    private VisualElement _charge;
    private VisualElement _fill;
    private VisualElement _tick;
    private Label _label;

    private VisualElement _pickupPrompt;
    private Label _pickupItemName;
    private Label _pickupHint;

    private VisualElement _resourceBar;
    private Label _resourceBarName;
    private VisualElement _resourceBarFill;

    // Linger: keep the bar visible briefly after the node leaves hover range.
    private const float ResourceBarLingerDuration = 1.2f;
    private float _resourceBarLinger;
    // Track the last known node so we can read its health during linger.
    private ResourceNode _lastResourceNode;

    private Camera _cam;

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
        _cam = Camera.main;
    }

    private bool _bound;

    private void OnEnable()
    {
        _bound = false;
        TryBind();      // may fail this frame if UIDocument hasn't built its tree yet — Update retries.
    }

    /// <summary>
    /// Resolve the UXML elements once the UIDocument has built its visual tree. rootVisualElement's
    /// children may not exist during OnEnable depending on component OnEnable order, so retry from Update.
    /// </summary>
    private void TryBind()
    {
        if (_doc == null) return;
        var root = _doc.rootVisualElement;
        if (root == null) return;

        _charge = root.Q<VisualElement>("Charge");
        if (_charge == null) return;    // tree not populated yet — try again next frame.

        _cursor = root.Q<VisualElement>("Cursor");
        _fill   = root.Q<VisualElement>("ChargeFill");
        _tick   = root.Q<VisualElement>("ChargeTick");
        _label  = root.Q<Label>("ChargeLabel");

        _pickupPrompt   = root.Q<VisualElement>("PickupPrompt");
        _pickupItemName = root.Q<Label>("PickupItemName");
        _pickupHint     = root.Q<Label>("PickupHint");

        _resourceBar     = root.Q<VisualElement>("ResourceBar");
        _resourceBarName = root.Q<Label>("ResourceBarName");
        _resourceBarFill = root.Q<VisualElement>("ResourceBarFill");

        // Start hidden; we use visibility (not display) so resolved dimensions are
        // always available for transform-based centering.
        if (_pickupPrompt != null) _pickupPrompt.style.visibility = Visibility.Hidden;
        if (_resourceBar  != null) _resourceBar.style.visibility  = Visibility.Hidden;

        // The on-screen aim reticle is retired in favour of a hardware cursor texture
        // (see CustomCursor) — hide the UI element so it doesn't double up with the OS cursor.
        if (_cursor != null) _cursor.style.display = DisplayStyle.None;

        _bound = true;
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (!_bound) { TryBind(); if (!_bound) return; }

        UpdateCharge();
        UpdatePickupPrompt();
        UpdateResourceHealthBar();
    }

    private void UpdateCharge()
    {
        bool swinging = _swing != null && _swing.IsCharging;
        bool throwing = _throw != null && _throw.IsCharging;
        bool charging = swinging || throwing;

        _charge.style.display = charging ? DisplayStyle.Flex : DisplayStyle.None;
        if (!charging) return;

        float charge = swinging ? _swing.ChargeNormalized : _throw.ChargeNormalized;
        _fill.style.width = Mathf.Clamp01(charge) * TrackInner;
        _label.text = (swinging ? "SWING " : "THROW ") + Mathf.RoundToInt(charge * 100f) + "%";

        _tick.style.display = swinging ? DisplayStyle.Flex : DisplayStyle.None;
        if (swinging)
        {
            float thr = _config != null ? _config.heavySwingThreshold : 0.6f;
            _tick.style.left = thr * TrackInner;
        }
    }

    private void UpdatePickupPrompt()
    {
        if (_pickupPrompt == null || _pickupPrompt.panel == null) return;

        WorldItemPickup hovered = _aimer != null ? _aimer.HoveredPickup : null;

        if (hovered == null || hovered.Item == null)
        {
            _pickupPrompt.style.visibility = Visibility.Hidden;
            return;
        }

        // Position above the pickup's world position.
        if (_cam != null)
        {
            Vector3 worldPos  = hovered.transform.position;
            Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
            // Behind the camera — hide.
            if (screenPos.z < 0f)
            {
                _pickupPrompt.style.visibility = Visibility.Hidden;
                return;
            }

            // Flip Y to match panel space (screen Y is bottom-up, panel Y is top-down).
            screenPos.y = Screen.height - screenPos.y;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_pickupPrompt.panel, new Vector2(screenPos.x, screenPos.y));

            // resolvedStyle dimensions are always valid because we use visibility, not display.
            float w = _pickupPrompt.resolvedStyle.width;
            float h = _pickupPrompt.resolvedStyle.height;
            // transform.position only triggers a repaint, not a layout pass.
            _pickupPrompt.transform.position = new Vector3(panelPos.x - w * 0.5f, panelPos.y - h - _pickupPromptOffsetY, 0f);
        }

        _pickupItemName.text = hovered.Item.displayName;
        _pickupPrompt.style.visibility = Visibility.Visible;
    }

    private void UpdateResourceHealthBar()
    {
        if (_resourceBar == null || _resourceBar.panel == null) return;

        ResourceNode node = _aimer != null ? _aimer.HoveredResource : null;

        // When a new node enters hover range, latch it and reset linger.
        if (node != null)
        {
            _lastResourceNode = node;
            _resourceBarLinger = ResourceBarLingerDuration;
        }
        else if (_resourceBarLinger > 0f)
        {
            _resourceBarLinger -= Time.deltaTime;
        }

        // Choose which node to display: live hover takes priority, then lingering last node.
        ResourceNode display = node ?? (_resourceBarLinger > 0f ? _lastResourceNode : null);

        if (display == null)
        {
            _resourceBar.style.visibility = Visibility.Hidden;
            return;
        }

        // Position above the node in world space.
        if (_cam != null)
        {
            Vector3 screenPos = _cam.WorldToScreenPoint(display.transform.position);
            if (screenPos.z < 0f)
            {
                _resourceBar.style.visibility = Visibility.Hidden;
                return;
            }

            screenPos.y = Screen.height - screenPos.y;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_resourceBar.panel, new Vector2(screenPos.x, screenPos.y));
            float w = _resourceBar.resolvedStyle.width;
            float h = _resourceBar.resolvedStyle.height;
            _resourceBar.transform.position = new Vector3(panelPos.x - w * 0.5f, panelPos.y - h - 48f, 0f);
        }

        // Fill fraction = currentHealth / maxHealth.
        // Fallback: if no config is assigned, use 1 as max so the bar is visible (one-hit node).
        int maxHp  = display.Config != null ? display.Config.maxHealth : 1;
        int curHp  = display.CurrentHealth;
        float frac = maxHp > 0 ? Mathf.Clamp01((float)curHp / maxHp) : 0f;

        // Track inner width: the CSS track is 80px, borders are 1px each side.
        const float TrackInnerW = 78f;
        _resourceBarFill.style.width = frac * TrackInnerW;
        _resourceBarName.text        = display.DisplayName.ToUpperInvariant();

        _resourceBar.style.visibility = Visibility.Visible;
    }
}
