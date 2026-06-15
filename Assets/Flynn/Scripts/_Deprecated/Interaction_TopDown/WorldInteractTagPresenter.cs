using UnityEngine;
using Flynn.Events;

/// <summary>
/// Drives the <see cref="InteractTagPanel"/> from the hover source. Reads
/// <see cref="PlayerMouseAimer.HoverResult"/> each frame and positions the
/// panel at the interactable's world anchor with its prompt text. When nothing is
/// hovered the tag is hidden. Also supports scan progress prompts.
///
/// Subscribes to <see cref="ScanProgressed"/> events from the GameEventBus to
/// show scan progress feedback when scanning an artifact.
/// </summary>
public class WorldInteractTagPresenter : MonoBehaviour
{
    [SerializeField] private PlayerMouseAimer _aimer;
    [SerializeField] private InteractTagPanel _tagPanel;

    private ScanTarget _activeScanTarget;
    private float _lastScanProgress;

    private void Awake()
    {
        if (_aimer == null) _aimer = FindObjectOfType<PlayerMouseAimer>();
        if (_tagPanel == null) _tagPanel = FindObjectOfType<InteractTagPanel>();
    }

    private void OnEnable()
    {
        if (GameEventBus.Instance != null)
            GameEventBus.Instance.Subscribe<ScanProgressed>(OnScanProgressed);
    }

    private void OnDisable()
    {
        if (GameEventBus.Instance != null)
            GameEventBus.Instance.Unsubscribe<ScanProgressed>(OnScanProgressed);
        _activeScanTarget = null;
    }

    private void LateUpdate()
    {
        if (_aimer == null || _tagPanel == null) return;

        // Check for active scan progress first (overrides normal prompt)
        if (_activeScanTarget != null && _lastScanProgress > 0f && _lastScanProgress < 1f)
        {
            int pct = Mathf.RoundToInt(_lastScanProgress * 100);
            _tagPanel.Show($"[F]  Scanning... {pct}%");
            return;
        }

        // Read from the unified hover result
        var result = _aimer.HoverResult;
        IInteractionPromptProvider hovered = result.Interactable;

        // Interface refs to destroyed Unity objects bypass the fake-null check.
        if (hovered is UnityEngine.Object obj && obj == null) hovered = null;

        if (hovered != null && hovered.TryGetPrompt(out InteractionPrompt prompt) && prompt.IsValid)
        {
            _tagPanel.Show(prompt.Display);
        }
        else
        {
            _tagPanel.HideTag();
        }
    }

    private void OnScanProgressed(ScanProgressed evt)
    {
        if (evt.Target == null) return;
        _activeScanTarget = evt.Target;
        _lastScanProgress = evt.Percent;
    }
}
