using UnityEngine;
using UnityEngine.Tilemaps;
using Flynn.Common;

namespace Flynn.Player
{
    /// <summary>
    /// One elevated tilemap layer. Uses tile-position polling (<see cref="Tilemap.HasTile"/>)
    /// instead of trigger colliders to determine whether the player is underneath.
    ///
    /// Manages two things:
    ///   • Occlusion — when the player is UNDER this layer (player level &lt; this level AND
    ///     a tile exists at the player's cell), fade the layer's sprites/tilemap so the player
    ///     stays visible; opaque when on/above it or outside the tile footprint.
    ///   • Fall prevention — the layer's solid collider blocks the player only while they're ON
    ///     this layer (player level == this level); it's ignored while they walk under.
    ///
    /// Designer setup:
    ///   1. Paint tiles on the elevated tilemap.
    ///   2. Add TilemapCollider2D (usedByComposite) + CompositeCollider2D (solid) for the floor.
    ///   3. Add this component, set _level, assign _tilemaps, _playerAnchor, _solidCollider.
    /// No trigger collider needed — tiles define the footprint.
    ///
    /// <see cref="Level"/> stays exposed so <see cref="LandingResolver"/> can resolve jump landings.
    /// </summary>
    public class ElevationZone : MonoBehaviour
    {
        [Header("Layer")]
        [Tooltip("Elevation tier of this layer. Ground = 0, first platform = 1, ...")]
        [SerializeField] private int _level = 1;

        [Header("Player reference")]
        [Tooltip("PlayerAnchor SO — set by PlayerAnchorRegistrar on the player at runtime.")]
        [SerializeField] private PlayerAnchor _playerAnchor;

        [Header("Occlusion (fade while player is underneath)")]
        [Tooltip("Sprites to fade. Empty = none.")]
        [SerializeField] private SpriteRenderer[] _sprites;
        [Tooltip("Tilemaps to fade via Tilemap.color AND to poll for tile presence.\n" +
                 "Assign the layer tilemap(s) explicitly.")]
        [SerializeField] private Tilemap[] _tilemaps;
        [Tooltip("Alpha multiplier while the player is underneath (0 = invisible, 1 = no fade).")]
        [Range(0f, 1f)][SerializeField] private float _fadedAlpha = 0.3f;
        [Tooltip("Fade rate (alpha units/sec). Higher = snappier.")]
        [SerializeField] private float _fadeSpeed = 8f;

        [Header("Fall prevention (optional)")]
        [Tooltip("The layer's solid collider (e.g. its CompositeCollider2D). Player collides with it\n" +
                 "only while standing ON this layer; ignored while walking under. Leave null to skip.")]
        [SerializeField] private Collider2D _solidCollider;

        public int Level => _level;

        // Authored (opaque) alphas, so we fade relative to them
        private float[] _spriteBaseA;
        private float[] _tilemapBaseA;
        private float _mul = 1f;     // current alpha multiplier
        private float _target = 1f;  // 1 = opaque, _fadedAlpha = faded

        // Cached player-side references
        private PlayerHeightState _playerHeight;
        private Collider2D _playerCollider;
        private bool _wasUnder;      // tracks state changes to avoid redundant IgnoreCollision calls

        private void Awake()
        {
            int sn = _sprites != null ? _sprites.Length : 0;
            _spriteBaseA = new float[sn];
            for (int i = 0; i < sn; i++) _spriteBaseA[i] = _sprites[i] ? _sprites[i].color.a : 1f;

            int tn = _tilemaps != null ? _tilemaps.Length : 0;
            _tilemapBaseA = new float[tn];
            for (int i = 0; i < tn; i++) _tilemapBaseA[i] = _tilemaps[i] ? _tilemaps[i].color.a : 1f;
        }

        private void Update()
        {
            // Resolve player references lazily
            if (_playerHeight == null)
            {
                var player = _playerAnchor != null ? _playerAnchor.Current : null;
                if (player == null) return;
                _playerHeight = player.GetComponent<PlayerHeightState>();
                if (_playerHeight == null) return;
            }
            if (_playerCollider == null && _playerHeight != null)
            {
                _playerCollider = _playerHeight.GetComponent<Collider2D>();
                if (_playerCollider == null) return;
            }

            Vector3 playerPos = _playerAnchor.Current.position;

            // Poll tilemaps: is there a tile at the player's cell?
            bool playerUnder = false;
            if (_tilemaps != null)
            {
                for (int i = 0; i < _tilemaps.Length; i++)
                {
                    if (!_tilemaps[i]) continue;
                    Vector3Int cell = _tilemaps[i].WorldToCell(playerPos);
                    if (_tilemaps[i].HasTile(cell))
                    {
                        playerUnder = true;
                        break;
                    }
                }
            }

            // Player is at or below this layer — ignore collision so they can walk on top AND underneath.
            // In 2D top-down, a solid collider blocks from all directions; we can't use it as a "floor"
            // to stand on. Fall prevention must be handled by edge detection elsewhere.
            bool shouldIgnoreCollision = _playerHeight.HeightLevel <= _level;

            // Only fade if the player is strictly below AND a tile is actually above them
            bool strictlyBelow = _playerHeight.HeightLevel < _level;
            _target = (playerUnder && strictlyBelow) ? _fadedAlpha : 1f;

            // Toggle solid collider on height-level change (avoids per-frame IgnoreCollision calls)
            if (shouldIgnoreCollision != _wasUnder)
            {
                _wasUnder = shouldIgnoreCollision;
                if (_solidCollider != null)
                    Physics2D.IgnoreCollision(_playerCollider, _solidCollider, shouldIgnoreCollision);
            }

            // Ease alpha toward target
            if (!Mathf.Approximately(_mul, _target))
            {
                _mul = Mathf.MoveTowards(_mul, _target, _fadeSpeed * Time.deltaTime);
                ApplyFade();
            }
        }

        private void ApplyFade()
        {
            for (int i = 0; i < _spriteBaseA.Length; i++)
            {
                if (!_sprites[i]) continue;
                Color c = _sprites[i].color; c.a = _spriteBaseA[i] * _mul; _sprites[i].color = c;
            }
            for (int i = 0; i < _tilemapBaseA.Length; i++)
            {
                if (!_tilemaps[i]) continue;
                Color c = _tilemaps[i].color; c.a = _tilemapBaseA[i] * _mul; _tilemaps[i].color = c;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Indicate which tilemaps are being polled
            if (_tilemaps == null) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.4f);
            foreach (var tm in _tilemaps)
            {
                if (tm == null) continue;
                var bounds = tm.localBounds;
                bounds.center = tm.transform.TransformPoint(bounds.center);
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
#endif
    }
}
