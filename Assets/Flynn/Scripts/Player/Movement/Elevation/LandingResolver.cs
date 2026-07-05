using UnityEngine;

namespace Flynn.Player
{
    /// <summary>
    /// Validates jump landings against the isometric grid: snaps a candidate world point to the
    /// nearest cell centre and confirms there is a walkable surface (an <see cref="ElevationZone"/>
    /// or ground collider) there, returning its elevation level. Used by PlayerJumpController for
    /// gap-cross and traversal jumps — if this returns false the jump is cancelled / downgraded.
    /// </summary>
    public class LandingResolver : MonoBehaviour
    {
        [Tooltip("The isometric Grid used to snap landing points to cell centres. Optional (raw point used if null).")]
        [SerializeField] private Grid _grid;
        [Tooltip("Layers that count as a walkable landing surface (ground tiles + platform tops).")]
        [SerializeField] private LayerMask _walkableMask = ~0;
        [Tooltip("Overlap probe radius at the snapped landing point.")]
        [SerializeField] private float _probeRadius = 0.15f;

        /// <summary>
        /// True if <paramref name="worldTarget"/> resolves to a walkable surface.
        /// <paramref name="snapped"/> = the target point (no grid snapping — caller already computes the adjacent tile);
        /// <paramref name="level"/> = that surface's elevation tier (from an ElevationZone, else <paramref name="fallbackLevel"/>).
        /// </summary>
        public bool TryResolveLanding(Vector2 worldTarget, int fallbackLevel, out Vector2 snapped, out int level)
        {
            snapped = worldTarget;
            level = fallbackLevel;

            // Prefer an ElevationZone at the landing (tells us the exact tier)
            var zone = FindZoneAt(snapped);
            if (zone != null)
            {
                level = zone.Level;
                return true;
            }

            // Otherwise any walkable collider = a valid same/fallback-level landing
            Collider2D hit = Physics2D.OverlapCircle(snapped, _probeRadius, _walkableMask);
            return hit != null;
        }

        private ElevationZone FindZoneAt(Vector2 point)
        {
            var hits = Physics2D.OverlapPointAll(point);
            for (int i = 0; i < hits.Length; i++)
            {
                var z = hits[i].GetComponent<ElevationZone>();
                if (z != null) return z;
            }
            return null;
        }
    }
}
