using UnityEngine;

namespace Flynn.Platforming
{
    /// <summary>
    /// Mark a floating platform that should fade out when the player passes behind/
    /// under it. On the tilted camera a higher platform visually overlaps lower
    /// content, so when the player is underneath we fade the platform to keep them
    /// visible.
    ///
    /// This component is intentionally dumb: it only exposes geometry + the sprites
    /// to fade and registers itself. All decision-making and fading happens in one
    /// place — <see cref="PlatformOcclusionManager"/> — so platforms stay cheap.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class OccludablePlatform : MonoBehaviour
    {
        [Tooltip("Sprites to fade. Leave empty to auto-collect from this object + children.")]
        [SerializeField] private SpriteRenderer[] _renderers;

        private Collider2D _collider;

        public Collider2D Collider => _collider != null ? _collider : (_collider = GetComponent<Collider2D>());

        /// <summary>World Y of the platform's top surface (where a player stands).</summary>
        public float SurfaceY => Collider.bounds.max.y;
        public float MinX => Collider.bounds.min.x;
        public float MaxX => Collider.bounds.max.x;
        public SpriteRenderer[] Renderers => _renderers;

        private void Reset() => _renderers = GetComponentsInChildren<SpriteRenderer>();

        private void Awake()
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            var manager = FindObjectOfType<PlatformOcclusionManager>();
            if (manager != null) manager.Register(this);
        }

        private void OnDisable()
        {
            var manager = FindObjectOfType<PlatformOcclusionManager>();
            if (manager != null) manager.Unregister(this);
        }
    }
}
