using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UGUI replacement for WrenchHudController. Lives on a Screen Space – Overlay Canvas.
/// All positioning is pure RectTransform mutation — zero UI layout passes per frame.
///
/// Hierarchy expected (built by the editor setup script):
///   WrenchHud_Canvas  (Canvas + this component)
///     CursorRoot        ← follows Input.mousePosition every frame
///       CursorBox       ← white 22×22 outer, black 20×20 inner (wireframe box)
///       CursorDot       ← white 2×2
///       Charge          ← hidden unless charging; child of cursor so it moves with it
///         ChargeTrack   ← white outer / black inner progress track
///           ChargeFill  ← left-anchored fill, width = charge × 58px
///           ChargeTick  ← 1px vertical mark at the light/heavy threshold
///         ChargeLabel   ← "SWING 73%" etc.
///     PickupPrompt      ← world-anchored via WorldToScreenPoint, hidden when nothing hovered
///       BG_Border / BG_Fill
///       ItemName        ← hovered item's displayName
///       HotkeyHint      ← "[E]  Pick Up"
/// </summary>
[RequireComponent(typeof(Canvas))]
public class WrenchHudUGUI : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private WrenchSwingController _swing;
    [SerializeField] private WrenchThrowController _throw;
    [SerializeField] private WrenchConfig          _config;
    [SerializeField] private PlayerMouseAimer      _aimer;

    [Header("Cursor")]
    [SerializeField] private RectTransform _cursorRoot;
    [Tooltip("Dedicated single-element Canvas for the cursor. Must be separate from the\n" +
             "charge/prompt canvas so cursor movement never triggers a TMP mesh rebuild.")]
    [SerializeField] private Canvas _cursorCanvas;
    [Tooltip("Hide the OS hardware cursor and use the on-screen reticle instead.")]
    [SerializeField] private bool _hideHardwareCursor = false;

    [Header("Charge Meter")]
    [SerializeField] private GameObject        _chargePanel;
    [SerializeField] private RectTransform     _chargeFill;
    [SerializeField] private RectTransform     _chargeTick;
    [SerializeField] private TextMeshProUGUI   _chargeLabel;

    [Header("Pickup Prompt")]
    [SerializeField] private GameObject        _pickupPromptRoot;
    [SerializeField] private RectTransform     _pickupPromptRect;
    [SerializeField] private TextMeshProUGUI   _pickupItemName;
    [Tooltip("Pixels above the item's screen position to place the prompt.")]
    [SerializeField] private float _pickupPromptOffsetY = 60f;

    // Track inner width matches the 58px interior of the 60px track (1px border each side).
    private const float TrackInner = 58f;

    private Camera _cam;

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake() => _cam = Camera.main;

    private void OnEnable()
    {
        if (_hideHardwareCursor) Cursor.visible = false;
    }

    private void OnDisable()
    {
        if (_hideHardwareCursor) Cursor.visible = true;
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;
        // Charge and pickup prompt run in Update — frame-rate accurate is fine for them.
        UpdateCharge();
        UpdatePickupPrompt();
    }

    // Cursor runs in LateUpdate so it samples Input as close to the render call as
    // possible, minimising the perceived gap between OS pointer and on-screen crosshair.
    private void LateUpdate()
    {
        UpdateCursor();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void UpdateCursor()
    {
        if (_cursorRoot == null) return;
        // anchoredPosition on a bottom-left anchored RT in a SS-Overlay canvas is a
        // direct pixel write — no world-space conversion, no canvas layout pass.
        _cursorRoot.anchoredPosition = Input.mousePosition;
    }

    private void UpdateCharge()
    {
        if (_chargePanel == null) return;

        bool swinging = _swing != null && _swing.IsCharging;
        bool throwing  = _throw  != null && _throw.IsCharging;
        bool charging  = swinging || throwing;

        _chargePanel.SetActive(charging);
        if (!charging) return;

        float charge = swinging ? _swing.ChargeNormalized : _throw.ChargeNormalized;

        // Drive fill width: anchoredPosition is left-anchored so sizeDelta.x == pixel width.
        _chargeFill.sizeDelta = new Vector2(Mathf.Clamp01(charge) * TrackInner, _chargeFill.sizeDelta.y);
        _chargeLabel.text = (swinging ? "SWING " : "THROW ") + Mathf.RoundToInt(charge * 100f) + "%";

        if (_chargeTick != null)
        {
            _chargeTick.gameObject.SetActive(swinging);
            if (swinging)
            {
                float thr = _config != null ? _config.heavySwingThreshold : 0.6f;
                _chargeTick.anchoredPosition = new Vector2(thr * TrackInner, _chargeTick.anchoredPosition.y);
            }
        }
    }

    private void UpdatePickupPrompt()
    {
        if (_pickupPromptRoot == null) return;

        WorldItemPickup hovered = _aimer != null ? _aimer.HoveredPickup : null;

        if (hovered == null || hovered.Item == null)
        {
            _pickupPromptRoot.SetActive(false);
            return;
        }

        Vector3 screenPos = _cam.WorldToScreenPoint(hovered.transform.position);

        // Behind camera — hide.
        if (screenPos.z < 0f)
        {
            _pickupPromptRoot.SetActive(false);
            return;
        }

        // Position directly in screen space. No panel conversion needed.
        _pickupPromptRect.position = new Vector3(screenPos.x, screenPos.y + _pickupPromptOffsetY, 0f);
        _pickupItemName.text = hovered.Item.displayName;
        _pickupPromptRoot.SetActive(true);
    }
}
