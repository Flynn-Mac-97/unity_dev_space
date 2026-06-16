using System.Collections;
using UnityEngine;

/// <summary>
/// Hold Tab + aim with the mouse to scan a <see cref="ScanTarget"/>. While scanning,
/// a multi-segment LineRenderer beam draws crackling energy from the target into the
/// player with sinusoidal wobble and brightness pulses. A particle-like energy stream
/// is drawn by animating the beam's width and colour. Camera shakes with intensity
/// that ramps up as the scan progresses. Releasing Tab early resets progress; holding
/// until complete triggers the target's OnScanComplete event. Exposes IsScanning and
/// ScanProgress for the HUD scan-progress bar.
/// </summary>
[RequireComponent(typeof(PlayerMouseAimer))]
public class PlayerScanController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode _scanKey = KeyCode.Tab;

    [Header("Beam Shape")]
    [Tooltip("LineRenderer child used for the scan beam visual.")]
    [SerializeField] private LineRenderer _beamLine;
    [SerializeField] private int _segmentCount = 24;
    [SerializeField] private float _beamWidth = 0.1f;
    [SerializeField] private Color _beamColor = new Color(0.3f, 0.85f, 1f, 0.95f);

    [Header("Beam Animation")]
    [SerializeField] private float _wobbleFrequency = 6f;
    [SerializeField] private float _wobbleAmplitude = 0.12f;
    [SerializeField] private float _flowSpeed = 8f;
    [SerializeField] private float _pulseFrequency = 4f;
    [SerializeField] private float _pulseIntensity = 0.35f;

    [Header("Camera Shake")]
    [SerializeField] private float _shakeAmplitude = 0.08f;
    [SerializeField] private float _shakeFrequency = 25f;
    [SerializeField] private float _shakeRampDuration = 0.5f;

    [Header("Scan Completion Burst")]
    [SerializeField] private float _completionShakeAmplitude = 0.25f;
    [SerializeField] private float _completionShakeDuration = 0.5f;
    [SerializeField] private float _completionFlashDuration = 0.2f;

    private PlayerMouseAimer _aimer;
    private IPlayerVisual _animDriver;
    private ScanTarget _currentTarget;
    private bool _scanning;
    private float _elapsed;
    private Vector3 _camOriginalLocalPos;
    private bool _camCaptured;

    // Camera shake state (runs even after scanning stops for the completion burst).
    private float _shakeAmplitudeCurrent;
    private float _shakeDuration;
    private float _shakeTimer;
    private bool _shaking;

    // Completion burst.
    private bool _burstActive;

    public bool IsScanning => _scanning;
    public float ScanProgress => _currentTarget != null ? _currentTarget.Progress : 0f;
    public ScanTarget CurrentTarget => _currentTarget;
    public Vector3 ScanOrigin => _animDriver != null ? _animDriver.VisualCenter : transform.position;

    private void Awake()
    {
        _aimer = GetComponent<PlayerMouseAimer>();
        _animDriver = GetComponent<IPlayerVisual>();
        SetupBeam();
    }

    private void SetupBeam()
    {
        if (_beamLine == null) return;
        _beamLine.positionCount = _segmentCount;
        _beamLine.startWidth = _beamWidth;
        _beamLine.endWidth = _beamWidth * 0.3f;
        _beamLine.enabled = false;
        _beamLine.useWorldSpace = true;
        _beamLine.numCornerVertices = 2;
        _beamLine.numCapVertices = 2;

        if (_beamLine.sharedMaterial == null)
            _beamLine.sharedMaterial = new Material(Shader.Find("Unlit/Transparent"));
    }

    private void Update()
    {
        ScanTarget hovered = _aimer.HoveredScanTarget;

        if (Input.GetKey(_scanKey) && hovered != null && !hovered.IsComplete)
        {
            if (!_scanning || _currentTarget != hovered)
            {
                if (_currentTarget != null && _currentTarget != hovered)
                    _currentTarget.ResetProgress();
                _currentTarget = hovered;
                _scanning = true;
                _elapsed = 0f;
                CaptureCamOrigin();
            }

            _elapsed += Time.deltaTime;
            _currentTarget.Advance(Time.deltaTime);

            // Ramp camera shake intensity based on scan progress.
            float progress = _currentTarget.Progress;
            float ramp = Mathf.Clamp01(_elapsed / _shakeRampDuration);
            float intensity = ramp * (0.3f + 0.7f * progress);
            StartShake(_shakeAmplitude * intensity, float.MaxValue);

            if (_currentTarget.IsComplete)
            {
                // Scan finished — trigger the epic completion burst.
                _scanning = false;
                TriggerCompletionBurst();
                _currentTarget = null;
            }
        }
        else
        {
            if (_scanning && _currentTarget != null)
                _currentTarget.ResetProgress();
            _scanning = false;
            _currentTarget = null;
        }
    }

    private void LateUpdate()
    {
        UpdateBeam();
        UpdateCameraShake();
        UpdateCompletionBurst();
    }

    // ── Beam ────────────────────────────────────────────────────────────────

    private void UpdateBeam()
    {
        if (_beamLine == null) return;

        if (_scanning && _currentTarget != null)
        {
            _beamLine.enabled = true;

            Vector3 origin = ScanOrigin;
            Vector3 targetPos = _currentTarget.transform.position + Vector3.up * 0.4f;
            float progress = _currentTarget.Progress;
            float dist = Vector3.Distance(origin, targetPos);

            // Direction and perpendicular for wobble.
            Vector3 dir = (origin - targetPos).normalized;
            Vector3 perp = Vector3.Cross(dir, Vector3.up);
            if (perp.sqrMagnitude < 0.001f) perp = Vector3.Cross(dir, Vector3.right);
            perp = perp.normalized;

            // Build the beam path with animated wobble.
            for (int i = 0; i < _segmentCount; i++)
            {
                float t = (float)i / (_segmentCount - 1);

                // Base position: lerp from target to player.
                Vector3 pos = Vector3.Lerp(targetPos, origin, t);

                // Sinusoidal wobble perpendicular to the beam, flowing from target to player.
                // Multiple frequencies for organic feel.
                float phase1 = t * _wobbleFrequency - _elapsed * _flowSpeed;
                float phase2 = t * _wobbleFrequency * 2.3f - _elapsed * _flowSpeed * 1.7f;
                float wobble = Mathf.Sin(phase1) * 0.7f + Mathf.Sin(phase2) * 0.3f;

                // Wobble is stronger in the middle, pinched at endpoints.
                float envelope = Mathf.Sin(t * Mathf.PI);
                float amplitude = _wobbleAmplitude * envelope * (0.5f + 0.5f * progress);
                pos += perp * (wobble * amplitude);

                _beamLine.SetPosition(i, pos);
            }

            // Animated width: pulse along the beam for energy-stream feel.
            float baseWidth = _beamWidth * (0.6f + 0.4f * progress);
            float pulse = 1f + Mathf.Sin(_elapsed * _pulseFrequency) * _pulseIntensity * progress;
            _beamLine.startWidth = baseWidth * pulse;
            _beamLine.endWidth = baseWidth * pulse * 0.3f;

            // Colour: brighter as scan progresses; pulse alpha.
            float alphaPulse = 0.7f + 0.3f * Mathf.Sin(_elapsed * _pulseFrequency * 2f);
            float brightPulse = 1f + pulse * 0.3f;
            Color c = new Color(
                Mathf.Min(1f, _beamColor.r * brightPulse),
                Mathf.Min(1f, _beamColor.g * brightPulse),
                Mathf.Min(1f, _beamColor.b * brightPulse),
                _beamColor.a * alphaPulse * (0.5f + 0.5f * progress));
            _beamLine.startColor = c;
            _beamLine.endColor = new Color(c.r, c.g, c.b, c.a * 0.2f);
        }
        else if (!_burstActive)
        {
            _beamLine.enabled = false;
        }
    }

    // ── Camera Shake ──────────────────────────────────────────────────────────

    private void CaptureCamOrigin()
    {
        if (_camCaptured) return;
        var cam = Camera.main;
        if (cam != null)
        {
            _camOriginalLocalPos = cam.transform.localPosition;
            _camCaptured = true;
        }
    }

    private void StartShake(float amplitude, float duration)
    {
        _shakeAmplitudeCurrent = amplitude;
        _shakeDuration = duration;
        _shakeTimer = 0f;
        _shaking = true;
    }

    private void UpdateCameraShake()
    {
        if (!_shaking) return;

        var cam = Camera.main;
        if (cam == null) return;

        _shakeTimer += Time.deltaTime;

        // Decay: fade out as we approach the duration.
        float decay = 1f;
        if (_shakeDuration < float.MaxValue)
            decay = 1f - Mathf.Clamp01(_shakeTimer / _shakeDuration);

        if (decay <= 0f)
        {
            cam.transform.localPosition = _camOriginalLocalPos;
            _shaking = false;
            return;
        }

        float amp = _shakeAmplitudeCurrent * decay;
        float offsetX = Mathf.Sin(_shakeTimer * _shakeFrequency) * amp
                       + Mathf.Sin(_shakeTimer * _shakeFrequency * 1.7f) * amp * 0.3f;
        float offsetY = Mathf.Cos(_shakeTimer * _shakeFrequency * 1.3f) * amp * 0.7f
                       + Mathf.Sin(_shakeTimer * _shakeFrequency * 2.1f) * amp * 0.2f;

        cam.transform.localPosition = _camOriginalLocalPos + new Vector3(offsetX, offsetY, 0f);
    }

    // ── Completion Burst ─────────────────────────────────────────────────────

    private void TriggerCompletionBurst()
    {
        _burstActive = true;

        // Epic camera shake on completion.
        StartShake(_completionShakeAmplitude, _completionShakeDuration);

        // Flash the beam super-bright then fade.
        if (_beamLine != null)
        {
            _beamLine.enabled = true;
            Color flash = new Color(1f, 1f, 1f, 1f);
            _beamLine.startColor = flash;
            _beamLine.endColor = new Color(1f, 1f, 1f, 0.5f);
            _beamLine.startWidth = _beamWidth * 3f;
            _beamLine.endWidth = _beamWidth * 1.5f;
        }

        StartCoroutine(CompletionBurstRoutine());
    }

    private IEnumerator CompletionBurstRoutine()
    {
        // Brief bright hold.
        yield return new WaitForSeconds(0.06f);

        float t = 0f;
        while (t < _completionFlashDuration)
        {
            t += Time.deltaTime;
            float fade = 1f - t / _completionFlashDuration;

            if (_beamLine != null)
            {
                float w = _beamWidth * 3f * fade;
                _beamLine.startWidth = w;
                _beamLine.endWidth = w * 0.5f;
                Color c = new Color(
                    _beamColor.r + (1f - _beamColor.r) * fade,
                    _beamColor.g + (1f - _beamColor.g) * fade,
                    _beamColor.b + (1f - _beamColor.b) * fade,
                    fade);
                _beamLine.startColor = c;
                _beamLine.endColor = new Color(c.r, c.g, c.b, c.a * 0.3f);
            }
            yield return null;
        }

        if (_beamLine != null)
            _beamLine.enabled = false;

        _burstActive = false;
    }

    private void UpdateCompletionBurst()
    {
        // Nothing frame-driven needed; the coroutine handles the burst.
    }
}
