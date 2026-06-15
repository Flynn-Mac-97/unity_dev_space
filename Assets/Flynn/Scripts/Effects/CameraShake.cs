using Cinemachine;
using UnityEngine;

/// <summary>
/// Camera shake using Cinemachine Impulse. Generates a one-shot impulse
/// that the CinachineBrain picks up for a natural decaying shake.
/// Attach to the same GameObject as the CinemachineBrain (Main Camera).
/// </summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraShake : MonoBehaviour
{
    [SerializeField] private float _defaultIntensity = 0.3f;
    [SerializeField] private float _defaultDuration = 0.2f;

    private static CameraShake _instance;
    public static CameraShake Instance => _instance;

    private CinemachineImpulseSource _impulse;

    private void Awake()
    {
        _instance = this;
        _impulse = GetComponent<CinemachineImpulseSource>();
    }

    /// <summary>Shake with default intensity.</summary>
    public void Shake() => Shake(_defaultIntensity, _defaultDuration);

    /// <summary>Shake with custom intensity and duration.</summary>
    public void Shake(float intensity, float duration)
    {
        if (_impulse == null) return;

        // Configure the impulse source for this hit
        _impulse.m_ImpulseDefinition.m_TimeEnvelope.m_AttackTime = 0.01f;
        _impulse.m_ImpulseDefinition.m_TimeEnvelope.m_SustainTime = duration * 0.3f;
        _impulse.m_ImpulseDefinition.m_TimeEnvelope.m_DecayTime = duration * 0.7f;

        // Random direction for natural feel
        var velocity = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-0.5f, 1f),
            Random.Range(-0.5f, 0.5f)
        ).normalized * intensity;

        _impulse.GenerateImpulse(velocity);
    }
}
