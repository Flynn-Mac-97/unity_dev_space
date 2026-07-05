using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using Flynn.Core;
using Flynn.Events;
using Flynn.Player.Combat;

namespace Flynn.Player
{
    /// <summary>
    /// Fixed-distance dash-jump. Space hops the player a set distance in the
    /// facing direction with a parabolic visual arc (PlayerHeightState.JumpOffset).
    /// Jump works anywhere; near elevation, LandingResolver upgrades the landing
    /// into a level transition (platform up/down). Blocked while the wrench acts.
    /// </summary>
    public class PlayerJumpController : MonoBehaviour
    {
        [Header("Jump")]
        [Tooltip("Dash length = one tile. Grid cell 1.0 x 0.5 iso at 0.5 tilemap scale -> 0.5 world units.")]
        [SerializeField] private float _dashDistance = 0.5f;
        [Tooltip("Peak height of the visual arc.")]
        [SerializeField] private float _arcHeight = 0.35f;
        [Tooltip("Air time in seconds.")]
        [SerializeField] private float _duration = 0.45f;
        [Tooltip("Y scaling on diagonal dashes — matches PlayerController2D movement.")]
        [SerializeField] private float _isoRatio = 0.5f;

        [Header("Landing Feel")]
        [Tooltip("Squash scale applied to the visual on landing (x wide, y flat).")]
        [SerializeField] private Vector2 _landSquash = new Vector2(1.12f, 0.85f);
        [SerializeField] private float _squashDuration = 0.12f;

        [Header("Tilemap (debug gizmo)")]
        [SerializeField] private Tilemap _tilemap;
        [SerializeField] private Vector2 _feetOffset = new Vector2(0f, -0.2f);
        [SerializeField] private float _directionalBias = 0.15f;

        private Rigidbody2D _rb;
        private PlayerController2D _player;
        private PlayerHeightState _height;
        private WrenchController _wrench;
        private LandingResolver _resolver;
        private Transform _visualRoot;

        private Vector2 _start;
        private Vector2 _target;
        private int _landLevel;
        private float _airTime;

        public bool IsAirborne { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _player = GetComponent<PlayerController2D>();
            _height = GetComponent<PlayerHeightState>();
            _wrench = GetComponent<WrenchController>();
            _resolver = FindObjectOfType<LandingResolver>();
            _visualRoot = transform.Find("VisualRoot");
        }

        private void Update()
        {
            if (IsAirborne)
            {
                _airTime += Time.deltaTime;
                float t = Mathf.Clamp01(_airTime / _duration);
                if (_height != null)
                    _height.JumpOffset = 4f * _arcHeight * t * (1f - t);
                if (t >= 1f) Land();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space)) TryJump();
        }

        private void FixedUpdate()
        {
            if (!IsAirborne) return;
            float t = Mathf.Clamp01(_airTime / _duration);
            _rb.MovePosition(Vector2.Lerp(_start, _target, t));
        }

        /// <summary>Attempt a jump. Public so tools/tests can trigger it.</summary>
        public bool TryJump()
        {
            if (IsAirborne) return false;
            if (_player == null || _player.IsMovementLocked) return false;
            if (_wrench != null && _wrench.IsActing) return false;

            Vector2 dir = _player.LastMoveDirection;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
            dir.Normalize();

            // Iso-correct dash: Y always compressed by the 2:1 cell ratio, so a jump
            // covers exactly one tile in every direction (1.0u across, 0.5u down-screen).
            Vector2 dash = dir * _dashDistance;
            dash.y *= _isoRatio;

            _start = transform.position;
            Vector2 rawTarget = _start + dash;

            // Near a traversal point the resolver snaps the landing and picks the
            // level (platform up / ledge down). Anywhere else: land where you aimed.
            int current = _height != null ? _height.HeightLevel : 0;
            if (_resolver != null && _resolver.TryResolveLanding(rawTarget, current, out Vector2 snapped, out int level))
            {
                _target = snapped;
                _landLevel = level;
            }
            else
            {
                _target = rawTarget;
                _landLevel = current;
            }

            IsAirborne = true;
            _airTime = 0f;
            _player.IsMovementLocked = true;
            _player.PlayTrigger(PlayerAnimationState.Jump, _duration);

            Publish(new PlayerJumped(transform.position));
            return true;
        }

        private void Land()
        {
            IsAirborne = false;
            if (_height != null)
            {
                _height.JumpOffset = 0f;
                if (_landLevel != _height.HeightLevel)
                    _height.SetLevel(_landLevel, snap: false);
            }

            _rb.position = _target;
            _player.IsMovementLocked = false;

            Publish(new PlayerLanded(transform.position));

            if (Flynn.Effects.HitImpactFX.Instance != null)
                Flynn.Effects.HitImpactFX.Instance.PlayAirWhoosh(transform.position, Vector2.down);

            if (_visualRoot != null)
                StartCoroutine(SquashRoutine());
        }

        private IEnumerator SquashRoutine()
        {
            Vector3 baseScale = _visualRoot.localScale;
            Vector3 squashed = new Vector3(baseScale.x * _landSquash.x, baseScale.y * _landSquash.y, baseScale.z);

            float t = 0f;
            while (t < _squashDuration)
            {
                t += Time.deltaTime;
                float p = t / _squashDuration;
                // Snap into squash instantly, ease back out.
                _visualRoot.localScale = Vector3.Lerp(squashed, baseScale, p * p);
                yield return null;
            }
            _visualRoot.localScale = baseScale;
        }

        private static void Publish<T>(T evt) where T : struct
        {
            if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
        }

        // ── Debug: ground-cell gizmo (kept from the original stub) ─────────

        public Vector3Int CurrentCell
        {
            get
            {
                if (_tilemap == null) return Vector3Int.zero;
                Vector3 feet = (Vector3)((Vector2)transform.position + _feetOffset);
                if (_rb != null && _rb.velocity.sqrMagnitude > 0.01f)
                    feet += (Vector3)(_rb.velocity.normalized * _directionalBias);
                return _tilemap.WorldToCell(feet);
            }
        }

        private void OnDrawGizmos()
        {
            if (_tilemap == null) return;
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();

            Vector3Int cell = CurrentCell;
            Vector3 center = _tilemap.GetCellCenterLocal(cell);
            Vector3 half = _tilemap.cellSize * 0.5f;

            Vector3 bottom = _tilemap.transform.TransformPoint(center + new Vector3(0, -half.y, 0));
            Vector3 right  = _tilemap.transform.TransformPoint(center + new Vector3(half.x, 0, 0));
            Vector3 top    = _tilemap.transform.TransformPoint(center + new Vector3(0, half.y, 0));
            Vector3 left   = _tilemap.transform.TransformPoint(center + new Vector3(-half.x, 0, 0));

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(bottom, right);
            Gizmos.DrawLine(right, top);
            Gizmos.DrawLine(top, left);
            Gizmos.DrawLine(left, bottom);
        }
    }
}
