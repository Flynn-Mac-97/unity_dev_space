using UnityEngine;


using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Common
{
    /// <summary>
    /// Shared configuration for the 2.5D sprite depth-sorting system.
    /// Assign one asset to both SpriteSortingManager (camera setup) and every
    /// SortableSprite (per-object order computation) so they stay in sync.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Sprite Sorting Config")]
    public class SpriteSortingConfigSO : ScriptableObject
    {
        [Tooltip(
            "World-space axis along which depth is measured — positive = further away.\n\n" +
            "Leave as (0,0,0) to let SpriteSortingManager derive it from the camera's\n" +
            "forward vector at runtime (recommended).\n\n" +
            "Manual examples:\n" +
            "  Pure Z top-down  : (0, 0, 1)\n" +
            "  45° tilted camera: (0, 0.71, 0.71)\n" +
            "  60° tilted camera: (0, 0.87, 0.50)")]
        [SerializeField] private Vector3 _sortAxis = Vector3.zero;

        [Tooltip("Number of sorting-order integers per world unit of depth.\n" +
                 "100 → 0.01 wu resolution (good for a 100-tile map).")]
        [Min(1f)]
        public float stepsPerUnit = 100f;

        [Tooltip("Constant added to every computed order.  Increase if you need " +
                 "sprite layers to sit above other non-sprite renderers in the scene.")]
        public int baseOrder = 0;

        // ── Runtime ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The normalised sort axis used at runtime.
        /// If _sortAxis is zero, falls back to Camera.main.transform.forward.
        /// SortableSprite and SpriteSortingManager both read this.
        /// </summary>
        public Vector3 SortAxisNormalized
        {
            get
            {
                if (_sortAxis.sqrMagnitude > 0.001f)
                    return _sortAxis.normalized;

                // Auto-derive from camera
                if (Camera.main != null)
                    return Camera.main.transform.forward;

                return Vector3.forward; // safe fallback
            }
        }

        /// <summary>Allows SpriteSortingManager to push the camera-derived axis back in.</summary>
        public void SetDerivedAxis(Vector3 axis)
        {
            if (_sortAxis.sqrMagnitude < 0.001f)
                return; // only override when axis is set to "auto"
            // No-op when the designer has set an explicit axis.
        }
    }

}
