using System;
using UnityEngine;

/// <summary>
/// Single source of truth for the mouse pointer. One per scene (lives on the UI manager object).
/// Every frame it reads the cursor, builds ONE world ray, and casts ONCE against a combined layer
/// mask — then caches the result. Gameplay and UI read these cached values instead of each running
/// their own <c>Camera.ScreenPointToRay(Input.mousePosition)</c> + <c>Physics.Raycast</c>.
///
/// Same hub idea as the other managers: record the signal once, everyone else just reads it. Runs
/// early (negative execution order) so the cache is fresh before any default-order consumer's Update.
/// </summary>
[DefaultExecutionOrder(-90)]
public class MousePointer : MonoBehaviour
{
    public static MousePointer Instance { get; private set; }

    [Tooltip("Camera the cursor ray is built from. Falls back to Camera.main.")]
    [SerializeField] private Camera _camera;
    [Tooltip("Combined mask for the per-frame world cast — the union of everything UI/gameplay needs.")]
    [SerializeField] private LayerMask _worldMask = ~0;
    [SerializeField] private float _maxDistance = 100f;

    // ── Cached each frame ───────────────────────────────────────────────────────

    /// <summary>Cursor position in screen pixels (== Input.mousePosition).</summary>
    public Vector2 ScreenPosition { get; private set; }
    /// <summary>Ray from the camera through the cursor.</summary>
    public Ray WorldRay { get; private set; }
    /// <summary>True if this frame's cast hit a collider on <see cref="_worldMask"/>.</summary>
    public bool HasHit { get; private set; }
    /// <summary>This frame's raycast hit (valid only when <see cref="HasHit"/>).</summary>
    public RaycastHit Hit { get; private set; }
    /// <summary>Hit point if we hit, else the ray projected to max distance (always usable as an aim target).</summary>
    public Vector3 WorldPoint { get; private set; }
    /// <summary>Collider under the cursor this frame, or null.</summary>
    public Collider HoverCollider { get; private set; }
    /// <summary>GameObject under the cursor this frame, or null.</summary>
    public GameObject HoverObject { get; private set; }

    /// <summary>Fires when the hovered GameObject changes (including to/from null).</summary>
    public event Action<GameObject> OnHoverChanged;

    private GameObject _lastHover;

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

        HasHit = Physics.Raycast(WorldRay, out RaycastHit hit, _maxDistance, _worldMask, QueryTriggerInteraction.Ignore);
        Hit = hit;

        if (HasHit)
        {
            WorldPoint = hit.point;
            HoverCollider = hit.collider;
            HoverObject = hit.collider != null ? hit.collider.gameObject : null;
        }
        else
        {
            WorldPoint = WorldRay.GetPoint(_maxDistance);
            HoverCollider = null;
            HoverObject = null;
        }

        if (HoverObject != _lastHover)
        {
            _lastHover = HoverObject;
            OnHoverChanged?.Invoke(HoverObject);
        }
    }
}
