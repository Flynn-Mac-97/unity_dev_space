using System.Collections.Generic;
using UnityEngine;

namespace Flynn.Platforming
{
    /// <summary>
    /// Single control point for platform occlusion fade. Each LateUpdate it checks
    /// every registered <see cref="OccludablePlatform"/> against the player: if the
    /// player is underneath a platform (horizontally within it, below its top, and
    /// not standing on it) the platform fades toward <see cref="_fadedAlpha"/> so
    /// the player stays visible; otherwise it returns to full opacity.
    ///
    /// Batch logic in ONE place — the platforms themselves stay dumb. Mirrors the
    /// Shadow2DManager pattern (register/unregister + iterate all in Tick).
    /// One per scene, on the MANAGERS object.
    /// </summary>
    [DefaultExecutionOrder(-88)]
    public class PlatformOcclusionManager : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float _fadedAlpha = 0.25f;
        [Tooltip("Alpha units per second while fading in/out.")]
        [SerializeField] private float _fadeSpeed = 8f;
        [Tooltip("Player must be at least this far below a platform's top to count as 'under' it.")]
        [SerializeField] private float _heightMargin = 0.25f;

        private readonly List<OccludablePlatform> _platforms = new();
        private readonly Dictionary<OccludablePlatform, float> _alpha = new();
        private PlatformerController2D _player;

        // ── Registration ──────────────────────────────────────────────────────

        public void Register(OccludablePlatform platform)
        {
            if (platform == null || _platforms.Contains(platform)) return;
            _platforms.Add(platform);
            _alpha[platform] = 1f;
        }

        public void Unregister(OccludablePlatform platform)
        {
            if (platform == null) return;
            _platforms.Remove(platform);
            _alpha.Remove(platform);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            foreach (var p in FindObjectsOfType<OccludablePlatform>())
                Register(p);
        }

        private void LateUpdate()
        {
            if (_player == null) _player = FindObjectOfType<PlatformerController2D>();
            if (_player == null) return;

            Vector2 feet = _player.transform.position;
            float dt = Time.deltaTime;

            for (int i = _platforms.Count - 1; i >= 0; i--)
            {
                var p = _platforms[i];
                if (p == null) { _platforms.RemoveAt(i); continue; }

                bool under =
                    feet.x >= p.MinX && feet.x <= p.MaxX &&     // horizontally beneath it
                    feet.y < p.SurfaceY - _heightMargin &&       // below its top surface
                    _player.CurrentPlatform != p.Collider;       // and not standing on it

                float target = under ? _fadedAlpha : 1f;
                float a = Mathf.MoveTowards(_alpha[p], target, _fadeSpeed * dt);
                _alpha[p] = a;
                ApplyAlpha(p, a);
            }
        }

        private static void ApplyAlpha(OccludablePlatform platform, float alpha)
        {
            var renderers = platform.Renderers;
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = renderers[i].color;
                c.a = alpha;
                renderers[i].color = c;
            }
        }
    }
}
