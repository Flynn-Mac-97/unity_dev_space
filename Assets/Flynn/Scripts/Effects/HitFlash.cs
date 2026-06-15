using System.Collections;
using UnityEngine;

/// <summary>
/// Flashes a GameObject white on hit by swapping its renderer color, then easing
/// back to the original. Works with both SpriteRenderer and MeshRenderer (via
/// material _Color). Static API so any system can trigger it.
/// </summary>
public static class HitFlash
{
    /// <summary>Flash the object white for the given duration.</summary>
    public static void Play(GameObject target, float duration = 0.1f)
    {
        if (target == null) return;
        var runner = FlashRunner.Get(target);
        runner.StartFlash(duration);
    }

    // ── Internal runner component ──────────────────────────────────────────

    private class FlashRunner : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private MeshRenderer _mr;
        private Material _mat; // instance material for MeshRenderer
        private Color _originalColor;
        private Coroutine _routine;

        public static FlashRunner Get(GameObject target)
        {
            var runner = target.GetComponent<FlashRunner>();
            if (runner == null)
            {
                runner = target.AddComponent<FlashRunner>();
                runner.hideFlags = HideFlags.HideAndDontSave;
            }
            return runner;
        }

        public void StartFlash(float duration)
        {
            // Cache renderer on first use
            if (_sr == null && _mr == null)
            {
                _sr = GetComponent<SpriteRenderer>();
                if (_sr == null)
                {
                    _mr = GetComponent<MeshRenderer>();
                    if (_mr != null)
                    {
                        _mat = _mr.material; // creates instance
                        _originalColor = _mat.GetColor("_Color");
                    }
                }
                else
                {
                    _originalColor = _sr.color;
                }
            }

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine(duration));
        }

        private IEnumerator FlashRoutine(float duration)
        {
            float t = 0f;

            // Quick white-in
            SetColor(Color.white);
            yield return null; // hold white for one frame

            // Ease back to original
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / duration;
                // Ease out cubic for smooth return
                float ease = 1f - (1f - p) * (1f - p) * (1f - p);
                SetColor(Color.Lerp(Color.white, _originalColor, ease));
                yield return null;
            }

            SetColor(_originalColor);
            _routine = null;
        }

        private void SetColor(Color c)
        {
            if (_sr != null) _sr.color = c;
            else if (_mat != null) _mat.SetColor("_Color", c);
        }

        private void OnDestroy()
        {
            // Restore original color if destroyed mid-flash
            SetColor(_originalColor);
        }
    }
}
