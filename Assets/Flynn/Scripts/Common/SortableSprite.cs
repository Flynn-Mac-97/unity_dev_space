using UnityEngine;

/// <summary>
/// Add to any world GameObject that needs dynamic depth-based sorting.
///
/// LateUpdate computes a sorting order from the object's depth along the
/// configured sort axis and stamps it onto all tracked SpriteRenderers.
///
/// Design intent
/// ─────────────
/// • One SortableSprite per logical object root (tree root, character root).
/// • Multiple SpriteRenderers on a single object (e.g. shadow + trunk + canopy)
///   are all set to the same order — they share a depth bucket.
/// • Use _relativeOrders[] offsets if layers within one object must be stacked
///   (e.g. shadow = base−1, trunk = base, canopy = base+1).
///
/// Foot-position sorting
/// ─────────────────────
/// Set _sortOriginOffset to the foot of the object (bottom of its bounding box)
/// for correct results when tall objects stand close together.
/// For a tree prefab with pivot at ground level this is Vector3.zero (default).
/// For a character with pivot at origin: Vector3.zero is already correct.
/// </summary>
public class SortableSprite : MonoBehaviour
{
    [SerializeField] private SpriteSortingConfigSO _config;

    [Tooltip("Rendered sprites to receive the computed sorting order.\n" +
             "Leave empty to auto-collect all SpriteRenderers in children on Awake.")]
    [SerializeField] private SpriteRenderer[] _renderers;

    [Tooltip("Per-renderer sorting-order offsets relative to the computed base.\n" +
             "Must match length of _renderers (or be empty for zero offsets).")]
    [SerializeField] private int[] _relativeOrders;

    [Tooltip("Local offset from this transform used as the depth-sample origin.\n" +
             "For ground-pivot objects: leave at (0,0,0).\n" +
             "For centre-pivot characters: try (0, -halfHeight, 0) to sample the feet.")]
    [SerializeField] private Vector3 _sortOriginOffset = Vector3.zero;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
    }

    /// <summary>
    /// Computes the current base sorting order for this object's depth position.
    /// Callers can add their own per-renderer offsets on top of this.
    /// </summary>
    public int ComputeBaseOrder()
    {
        if (_config == null) return 0;
        Vector3 sortPos = transform.position + transform.TransformDirection(_sortOriginOffset);
        float depth = Vector3.Dot(sortPos, _config.SortAxisNormalized);
        return _config.baseOrder - Mathf.RoundToInt(depth * _config.stepsPerUnit);
    }

    private void LateUpdate()
    {
        if (_config == null || _renderers == null) return;

        int baseOrder = ComputeBaseOrder();

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            int offset = (_relativeOrders != null && i < _relativeOrders.Length)
                ? _relativeOrders[i]
                : 0;
            _renderers[i].sortingOrder = baseOrder + offset;
        }
    }

    // ── Editor helpers ────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_config == null) return;

        // Show the sort-origin point
        Vector3 sortPos = transform.position + transform.TransformDirection(_sortOriginOffset);
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(sortPos, 0.08f);

        // Show depth arrow along sort axis
        Vector3 axis = _config.SortAxisNormalized;
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.5f);
        Gizmos.DrawRay(sortPos, axis * 1.5f);

        // Live order label via Handles
#if UNITY_EDITOR
        Vector3 sortPosWS = transform.position + transform.TransformDirection(_sortOriginOffset);
        float depth = Vector3.Dot(sortPosWS, axis);
        int order = _config.baseOrder - Mathf.RoundToInt(depth * _config.stepsPerUnit);
        UnityEditor.Handles.Label(
            sortPos + Vector3.up * 0.3f,
            $"order: {order}",
            new GUIStyle { normal = { textColor = new Color(1f, 0.9f, 0.2f) }, fontSize = 10 });
#endif
    }
#endif
}
