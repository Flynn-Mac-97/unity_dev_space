using UnityEngine;

namespace Flynn.Effects
{
    /// <summary>
    /// Camera shake by offsetting the Main Camera's position in LateUpdate
    /// (after CinemachineBrain has set it). Runs at high execution order
    /// so it always applies after Cinemachine.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float _defaultIntensity = 0.3f;
        [SerializeField] private float _defaultDuration = 0.25f;

        private static CameraShake _instance;
        public static CameraShake Instance => _instance;

        private float _shakeTime;
        private float _shakeDuration;
        private float _shakeIntensity;

        private void Awake() => _instance = this;

        public void Shake() => Shake(_defaultIntensity, _defaultDuration);

        public void Shake(float intensity, float duration)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTime = duration;
        }

        private void LateUpdate()
        {
            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.unscaledDeltaTime;
                float decay = Mathf.Max(0f, _shakeTime / _shakeDuration);
                float x = (Random.value - 0.5f) * 2f * _shakeIntensity * decay;
                float y = (Random.value - 0.5f) * 2f * _shakeIntensity * decay;
                transform.position += new Vector3(x, y, 0f);
            }
        }
    }
}
