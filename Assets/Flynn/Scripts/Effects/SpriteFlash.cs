using System.Collections;
using UnityEngine;

namespace Flynn.Effects
{
    /// <summary>
    /// Shader-driven white flash for sprites using the Flynn/Sprite shader.
    /// Animates _FlashAmount 1→0 over the duration. Call Flash() from any
    /// UnityEvent (e.g. ResourceNode _onHit) or code.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteFlash : MonoBehaviour
    {
        [SerializeField] private float _duration = 0.12f;

        private SpriteRenderer _sr;
        private Material _mat;
        private Coroutine _routine;

        private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _mat = _sr.material; // instance material
        }

        public void Flash() => Flash(0);

        public void Flash(int _)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine(_duration));
        }

        private IEnumerator FlashRoutine(float duration)
        {
            float t = 0f;
            _mat.SetFloat(FlashAmountID, 1f);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _mat.SetFloat(FlashAmountID, 1f - (t / duration));
                yield return null;
            }

            _mat.SetFloat(FlashAmountID, 0f);
            _routine = null;
        }
    }
}
