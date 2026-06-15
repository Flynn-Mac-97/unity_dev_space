using UnityEngine;
using Flynn.Events;

/// <summary>
/// A world object that the player can scan with a scanning tool. Tracks scan progress,
/// consumes battery while scanning, and reveals an item upon completion.
/// Implements both <see cref="IInteractionPromptProvider"/> (for hover UI) and
/// <see cref="IProgressInteractable"/> (for the scanning interaction system).
/// </summary>
public class ScanTarget : MonoBehaviour, IInteractionPromptProvider, IProgressInteractable
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Config")]
    [Tooltip("Configuration asset defining scan duration, battery cost, and reward.")]
    [SerializeField] private ScanTargetConfig _config;

    [Header("Prompt")]
    [Tooltip("Key binding displayed in the interaction prompt.")]
    [SerializeField] private string _promptKey = "E";

    [Tooltip("Verb displayed in the interaction prompt.")]
    [SerializeField] private string _promptVerb = "Scan";

    // ── Public properties ─────────────────────────────────────────────────────

    public ScanTargetConfig Config => _config;

    /// <summary>Current scan progress from 0 (not started) to 1 (complete).</summary>
    public float ScanProgress { get; private set; }

    /// <summary>Whether this target has been fully scanned.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Whether a scan is currently in progress.</summary>
    public bool IsScanning { get; private set; }

    // ── IProgressInteractable ─────────────────────────────────────────────────

    public float Progress => ScanProgress;

    /// <summary>
    /// Returns true when the target is not already completed and has a valid config.
    /// </summary>
    public bool CanStart()
    {
        return !IsCompleted && _config != null;
    }

    /// <summary>
    /// Called when a scan begins. Initialises progress and publishes ScanStarted.
    /// </summary>
    public void Begin()
    {
        if (IsCompleted) return;
        IsScanning = true;
        ScanProgress = 0f;
        PublishEvent(new ScanStarted(this));
    }

    /// <summary>
    /// Advances scan progress by <paramref name="deltaTime"/> / scanDuration.
    /// Publishes ScanProgressed events. Automatically completes when progress reaches 1.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!IsScanning || IsCompleted) return;

        ScanProgress = Mathf.Clamp01(ScanProgress + (deltaTime / _config.scanDuration));
        PublishEvent(new ScanProgressed(this, ScanProgress));

        if (ScanProgress >= 1f)
        {
            CompleteScan();
        }
    }

    /// <summary>
    /// Completes the scan. Called automatically by Tick or externally via Complete().
    /// </summary>
    public void Complete()
    {
        CompleteScan();
    }

    /// <summary>
    /// Cancels an in-progress scan. Resets progress to zero.
    /// </summary>
    public void Cancel()
    {
        if (!IsScanning) return;
        IsScanning = false;
        ScanProgress = 0f;
        IsCompleted = false;
    }

    // ── IInteractionPromptProvider ────────────────────────────────────────────

    /// <summary>
    /// Provides an interaction prompt when the target is scannable (not yet completed
    /// and has a valid config).
    /// </summary>
    public bool TryGetPrompt(out InteractionPrompt prompt)
    {
        if (!IsCompleted && _config != null)
        {
            string label = _config.displayName ?? name;
            prompt = new InteractionPrompt(_promptKey, _promptVerb, transform, label);
            return true;
        }

        prompt = default;
        return false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns true when the target can start a new scan.</summary>
    public bool CanStartScan() => CanStart();

    /// <summary>Begins the scanning process. Alias for Begin().</summary>
    public void StartScan()
    {
        Begin();
    }

    /// <summary>
    /// Ticks the scan forward by <paramref name="deltaTime"/> seconds.
    /// Alias for Tick() for code clarity in the scanning system.
    /// </summary>
    public void TickScan(float deltaTime)
    {
        Tick(deltaTime);
    }

    /// <summary>Completes the scan and publishes ScanCompleted.</summary>
    public void CompleteScan()
    {
        if (IsCompleted) return;

        IsScanning = false;
        IsCompleted = true;
        ScanProgress = 1f;

        // Grant the revealed item, if any.
        if (_config != null && _config.revealItem != null && PlayerInventory.Instance != null)
            PlayerInventory.Instance.TryAddItem(_config.revealItem, Mathf.Max(1, _config.revealCount));

        PublishEvent(new ScanCompleted(this));

        // Reveal authored info/lore lines.
        if (_config != null && _config.infoLines != null && _config.infoLines.Length > 0)
            PublishEvent(new ScanRevealed(this, _config.infoLines, transform.position));
    }

    /// <summary>Alias for Cancel().</summary>
    public void CancelScan()
    {
        Cancel();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void PublishEvent<T>(T evt) where T : struct
    {
        if (GameEventBus.Instance != null)
            GameEventBus.Instance.Publish(evt);
    }
}
