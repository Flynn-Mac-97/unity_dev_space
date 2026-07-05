using System.Collections;
using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Effects
{
    /// <summary>
    /// Brief time freeze on impact ("hit stop") to make swings feel weighty.
    /// Uses Time.timeScale so all game logic pauses, then eases back to 1.
    /// Scales duration with hit strength so heavy charged hits freeze longer.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        [SerializeField] private float _baseDuration = 0.04f;
        [SerializeField] private float _maxDuration = 0.1f;
        [SerializeField] private float _freezeScale = 0f;
        [SerializeField] private float _easeBackDuration = 0.06f;

        private static HitStop _instance;
        public static HitStop Instance => _instance;

        private Coroutine _routine;

        private void Awake() => _instance = this;

        /// <summary>Trigger hit stop with strength 0-1 (0 = tap, 1 = full charge).</summary>
        public void Play(float strength = 0.5f)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(HitStopRoutine(strength));
        }

        private IEnumerator HitStopRoutine(float strength)
        {
            float freezeDur = Mathf.Lerp(_baseDuration, _maxDuration, strength);
            Time.timeScale = _freezeScale;

            // Wait in unscaled time
            yield return new WaitForSecondsRealtime(freezeDur);

            // Ease back to full speed
            float t = 0f;
            while (t < _easeBackDuration)
            {
                t += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(_freezeScale, 1f, t / _easeBackDuration);
                yield return null;
            }

            Time.timeScale = 1f;
            _routine = null;
        }

        private void OnDisable()
        {
            if (_routine != null) Time.timeScale = 1f;
        }
    }

}
