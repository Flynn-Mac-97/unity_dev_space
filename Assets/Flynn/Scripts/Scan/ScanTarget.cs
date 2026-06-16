using System;
using UnityEngine;

/// <summary>
/// Marks a world object as scannable. Implements <see cref="IInteractionPromptProvider"/>
/// so the HUD shows a prompt when the cursor hovers it. Exposes events for scan progress
/// and completion, and holds the scan duration config.
/// </summary>
public class ScanTarget : MonoBehaviour, IInteractionPromptProvider
{
    [Tooltip("How long the player must hold the scan key to complete the scan.")]
    [SerializeField] private float _scanDuration = 2f;

    [Tooltip("Display name shown in the interaction prompt.")]
    [SerializeField] private string _displayName = "Unknown Object";

    /// <summary>Normalised scan progress (0–1).</summary>
    public float Progress { get; private set; }

    /// <summary>Whether this target has been fully scanned and destroyed.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>Fired every frame while scanning; parameter is normalised progress 0–1.</summary>
    public event Action<float> OnProgress;

    /// <summary>Fired once when the scan reaches 1.0.</summary>
    public event Action OnScanComplete;

    /// <summary>Seconds the player must hold scan to complete.</summary>
    public float ScanDuration => _scanDuration;

    /// <summary>
    /// Advance the scan by <paramref name="delta"/> seconds.
    /// Called by <see cref="PlayerScanController"/> while the key is held.
    /// </summary>
    public void Advance(float delta)
    {
        if (IsComplete) return;
        Progress = Mathf.Clamp01(Progress + delta / _scanDuration);
        OnProgress?.Invoke(Progress);

        if (Progress >= 1f)
        {
            IsComplete = true;
            OnScanComplete?.Invoke();
        }
    }

    /// <summary>Reset scan progress (e.g. player released the key early).</summary>
    public void ResetProgress()
    {
        if (IsComplete) return;
        Progress = 0f;
    }

    // ── IInteractionPromptProvider ──────────────────────────────────────────

    public bool TryGetPrompt(out InteractionPrompt prompt)
    {
        if (IsComplete)
        {
            prompt = default;
            return false;
        }
        prompt = new InteractionPrompt("Tab", "Scan", transform, _displayName);
        return true;
    }
}
