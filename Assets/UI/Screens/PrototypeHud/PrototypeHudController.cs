using System;
using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public class PrototypeHudController : MonoBehaviour
{
    public enum PlayerMode
    {
        Collect = 1,
        Restore = 2,
        Scan = 3
    }

    private const string HudRootName = "prototype-hud-root";

    private const string CollectButtonName = "mode-collect-button";
    private const string RestoreButtonName = "mode-restore-button";
    private const string ScanButtonName = "mode-scan-button";

    private const string CollectBadgeName = "mode-collect-badge";
    private const string RestoreBadgeName = "mode-restore-badge";
    private const string ScanBadgeName = "mode-scan-badge";

    private const string HudHiddenClassName = "hud-hidden";
    private const string HudVisibleClassName = "hud-visible";
    private const string ModeButtonActiveClassName = "mode-button--active";
    private const string ModeBadgeActiveClassName = "mode-badge--active";

    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private IntroSequencePlayer introSequencePlayer;
    [SerializeField] private bool autoFindIntroSequencePlayer = true;

    [Header("Startup")]
    [SerializeField] private PlayerMode initialMode = PlayerMode.Collect;

    private VisualElement _root;
    private VisualElement _hudRoot;

    // Required by brief: cache button elements with root.Q<VisualElement>().
    private VisualElement _collectButtonElement;
    private VisualElement _restoreButtonElement;
    private VisualElement _scanButtonElement;

    private VisualElement _collectBadge;
    private VisualElement _restoreBadge;
    private VisualElement _scanBadge;

    private Button _collectButton;
    private Button _restoreButton;
    private Button _scanButton;

    private Action _onCollectClicked;
    private Action _onRestoreClicked;
    private Action _onScanClicked;

    private bool _isBound;
    private bool _callbacksRegistered;
    private bool _isHudVisible;

    private PlayerMode _currentMode;

    private void Reset()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (introSequencePlayer == null)
            introSequencePlayer = FindFirstObjectByType<IntroSequencePlayer>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        TryBindUi();
    }

    private void OnEnable()
    {
        TryBindUi();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
        _callbacksRegistered = false;
        _isBound = false;
    }

    private void Update()
    {
        if (!TryBindUi())
            return;

        UpdateHudVisibility();

        if (!_isHudVisible)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            SetMode(PlayerMode.Collect);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            SetMode(PlayerMode.Restore);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            SetMode(PlayerMode.Scan);
    }

    private bool TryBindUi()
    {
        if (_isBound)
            return true;

        if (uiDocument == null)
        {
            Debug.LogError("[PrototypeHud] Missing UIDocument reference.");
            return false;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogError("[PrototypeHud] UIDocument rootVisualElement is null.");
            return false;
        }

        _hudRoot = _root.Q<VisualElement>(HudRootName);
        _collectButtonElement = _root.Q<VisualElement>(CollectButtonName);
        _restoreButtonElement = _root.Q<VisualElement>(RestoreButtonName);
        _scanButtonElement = _root.Q<VisualElement>(ScanButtonName);

        _collectBadge = _root.Q<VisualElement>(CollectBadgeName);
        _restoreBadge = _root.Q<VisualElement>(RestoreBadgeName);
        _scanBadge = _root.Q<VisualElement>(ScanBadgeName);

        _collectButton = _collectButtonElement as Button;
        _restoreButton = _restoreButtonElement as Button;
        _scanButton = _scanButtonElement as Button;

        if (_hudRoot == null || _collectButton == null || _restoreButton == null || _scanButton == null
            || _collectBadge == null || _restoreBadge == null || _scanBadge == null)
        {
            Debug.LogError("[PrototypeHud] Missing required UI element names. Check the PrototypeHud UXML names.");
            return false;
        }

        _hudRoot.EnableInClassList(HudVisibleClassName, false);
        _hudRoot.EnableInClassList(HudHiddenClassName, true);
        _isHudVisible = false;

        RegisterCallbacks();
        SetMode(initialMode);

        _isBound = true;
        return true;
    }

    private void RegisterCallbacks()
    {
        if (_callbacksRegistered)
            return;

        _onCollectClicked = () => SetMode(PlayerMode.Collect);
        _onRestoreClicked = () => SetMode(PlayerMode.Restore);
        _onScanClicked = () => SetMode(PlayerMode.Scan);

        _collectButton.clicked += _onCollectClicked;
        _restoreButton.clicked += _onRestoreClicked;
        _scanButton.clicked += _onScanClicked;

        _callbacksRegistered = true;
    }

    private void UnregisterCallbacks()
    {
        if (!_callbacksRegistered)
            return;

        if (_collectButton != null && _onCollectClicked != null)
            _collectButton.clicked -= _onCollectClicked;

        if (_restoreButton != null && _onRestoreClicked != null)
            _restoreButton.clicked -= _onRestoreClicked;

        if (_scanButton != null && _onScanClicked != null)
            _scanButton.clicked -= _onScanClicked;

        _onCollectClicked = null;
        _onRestoreClicked = null;
        _onScanClicked = null;
    }

    private void UpdateHudVisibility()
    {
        bool shouldShowHud = !IsIntroSequencePlaying();
        if (shouldShowHud == _isHudVisible)
            return;

        _isHudVisible = shouldShowHud;
        _hudRoot.EnableInClassList(HudVisibleClassName, shouldShowHud);
        _hudRoot.EnableInClassList(HudHiddenClassName, !shouldShowHud);
    }

    private bool IsIntroSequencePlaying()
    {
        if (introSequencePlayer == null && autoFindIntroSequencePlayer)
            introSequencePlayer = FindFirstObjectByType<IntroSequencePlayer>(FindObjectsInactive.Include);

        return introSequencePlayer != null && introSequencePlayer.IsSequencePlaying;
    }

    public void SetMode(PlayerMode mode)
    {
        _currentMode = mode;

        SetModeVisual(PlayerMode.Collect, isActive: false);
        SetModeVisual(PlayerMode.Restore, isActive: false);
        SetModeVisual(PlayerMode.Scan, isActive: false);

        SetModeVisual(mode, isActive: true);
    }

    private void SetModeVisual(PlayerMode mode, bool isActive)
    {
        switch (mode)
        {
            case PlayerMode.Collect:
                _collectButtonElement.EnableInClassList(ModeButtonActiveClassName, isActive);
                _collectBadge.EnableInClassList(ModeBadgeActiveClassName, isActive);
                break;

            case PlayerMode.Restore:
                _restoreButtonElement.EnableInClassList(ModeButtonActiveClassName, isActive);
                _restoreBadge.EnableInClassList(ModeBadgeActiveClassName, isActive);
                break;

            case PlayerMode.Scan:
                _scanButtonElement.EnableInClassList(ModeButtonActiveClassName, isActive);
                _scanBadge.EnableInClassList(ModeBadgeActiveClassName, isActive);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }
}
