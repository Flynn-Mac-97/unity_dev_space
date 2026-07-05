using System;
using UnityEngine;
using UnityEngine.EventSystems;


using Flynn.Core;
using Flynn.Events;

using Flynn.Player.Interaction;
namespace Flynn.UI.Core
{
    /// <summary>
    /// Single source of truth for the mouse pointer for a 2D isometric game (30° camera tilt).
    /// Every frame it reads the cursor, builds ONE world ray, and casts ONCE via
    /// <c>Physics2D.GetRayIntersection</c> — which accepts a 3D camera ray and resolves against
    /// 2D colliders correctly even when the camera is tilted. Caches the result so all gameplay
    /// and UI consumers read it instead of each running their own cast.
    ///
    /// When no 2D collider is hit, <see cref="WorldPoint"/> is the intersection of the ray with
    /// the isometric ground plane (Y = <see cref="_groundPlaneY"/>), giving a usable aim target
    /// at all times. Runs early (negative execution order) so the cache is fresh before any
    /// default-order consumer's Update.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class MousePointer : MonoBehaviour
    {
        public static MousePointer Instance { get; private set; }

        [Tooltip("Camera the cursor ray is built from. Falls back to Camera.main.")]
        [SerializeField] private Camera _camera;
        [Tooltip("Combined mask for the per-frame 2D cast — the union of everything UI/gameplay needs.")]
        [SerializeField] private LayerMask _worldMask = ~0;
        [SerializeField] private float _maxDistance = 100f;
        [Tooltip("Y height of the isometric ground plane. Used to compute WorldPoint when no 2D collider is hit.")]
        [SerializeField] private float _groundPlaneY = 0f;

        // ── Cached each frame ───────────────────────────────────────────────────────

        /// <summary>Cursor position in screen pixels (== Input.mousePosition).</summary>
        public Vector2 ScreenPosition { get; private set; }
        /// <summary>Ray from the camera through the cursor.</summary>
        public Ray WorldRay { get; private set; }
        /// <summary>True if this frame's cast hit a collider on <see cref="_worldMask"/>.</summary>
        public bool HasHit { get; private set; }
        /// <summary>This frame's 2D raycast hit (valid only when <see cref="HasHit"/>).</summary>
        public RaycastHit2D Hit { get; private set; }
        /// <summary>Hit point if a 2D collider was struck, else the intersection with the isometric ground plane (always usable as an aim target).</summary>
        public Vector3 WorldPoint { get; private set; }
        /// <summary>2D collider under the cursor this frame, or null.</summary>
        public Collider2D HoverCollider { get; private set; }
        /// <summary>GameObject under the cursor this frame, or null.</summary>
        public GameObject HoverObject { get; private set; }

        /// <summary>Fires when the hovered GameObject changes (including to/from null).</summary>
        public event Action<GameObject> OnHoverChanged;

        private GameObject _lastHover;

        // ── Private helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Intersects <paramref name="ray"/> with the horizontal ground plane at Y = <see cref="_groundPlaneY"/>.
        /// Falls back to the ray projected to max distance if the ray is nearly parallel to the ground
        /// (camera almost horizontal — shouldn't happen at 30° tilt but safe to guard).
        /// </summary>
        private Vector3 GroundPlanePoint(Ray ray)
        {
            float denom = ray.direction.y;
            if (Mathf.Abs(denom) > 1e-5f)
            {
                float t = (_groundPlaneY - ray.origin.y) / denom;
                if (t > 0f) return ray.GetPoint(t);
            }
            return ray.GetPoint(_maxDistance);
        }

        // ── Public helpers ──────────────────────────────────────────────────────────

        /// <summary>True when the current hover object sits on one of the given layers.</summary>
        public bool HoverOnLayers(LayerMask mask)
            => HoverObject != null && (mask.value & (1 << HoverObject.layer)) != 0;

        // ── Unity messages ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[MousePointer] Duplicate; destroying {name}.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            if (_camera == null) _camera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }

            ScreenPosition = Input.mousePosition;
            WorldRay = _camera.ScreenPointToRay(ScreenPosition);

            // Physics2D.GetRayIntersection takes a 3D ray — correct for isometric cameras that aren't
            // axis-aligned with the 2D physics plane.
            RaycastHit2D hit2D = Physics2D.GetRayIntersection(WorldRay, _maxDistance, _worldMask);

            // Suppress world hover when the cursor is over a UI element — gameplay/UI consumers
            // shouldn't "hover" a world object through a panel sitting on top of it.
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            HasHit = hit2D.collider != null && !overUI;
            Hit = hit2D;

            if (HasHit)
            {
                WorldPoint = hit2D.point;
                HoverCollider = hit2D.collider;
                HoverObject = hit2D.collider.gameObject;
            }
            else
            {
                WorldPoint = GroundPlanePoint(WorldRay);
                HoverCollider = null;
                HoverObject = null;
            }

            if (HoverObject != _lastHover)
            {
                _lastHover = HoverObject;
                OnHoverChanged?.Invoke(HoverObject);
                Debug.Log($"[MousePointer] Hover → {(HoverObject != null ? HoverObject.name : "null")}");
            }

        }
    }

}
