using UnityEngine;

namespace Flynn.Player
{
    /// <summary>
    /// Smooth ramp / stairs between two tiers. While the player overlaps, the elevation is driven
    /// continuously by how far along the ramp axis they are, so walking up reads as a gradual climb.
    /// Author by placing the trigger over the steps and pointing <see cref="_lowEnd"/> /
    /// <see cref="_highEnd"/> at the bottom and top of the run.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ElevationRamp : MonoBehaviour
    {
        [Tooltip("World marker at the bottom of the ramp (low level).")]
        [SerializeField] private Transform _lowEnd;
        [Tooltip("World marker at the top of the ramp (high level).")]
        [SerializeField] private Transform _highEnd;
        [SerializeField] private int _lowLevel = 0;
        [SerializeField] private int _highLevel = 1;

        private void Reset()
        {
            var c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            var h = other.GetComponentInParent<PlayerHeightState>();
            if (h == null || _lowEnd == null || _highEnd == null) return;

            Vector2 lo = _lowEnd.position;
            Vector2 hi = _highEnd.position;
            Vector2 axis = hi - lo;
            float len2 = axis.sqrMagnitude;
            if (len2 < 1e-5f) return;

            // Fraction of the way up the ramp (projected onto the ramp axis)
            Vector2 p = (Vector2)other.transform.position - lo;
            float t = Mathf.Clamp01(Vector2.Dot(p, axis) / len2);

            float worldY = Mathf.Lerp(_lowLevel, _highLevel, t) * h.UnitsPerLevel;
            // Discrete level snaps at the half-way point so collision/sort flip cleanly
            int level = t < 0.5f ? _lowLevel : _highLevel;
            h.SetContinuousElevation(worldY, level);
        }

        private void OnDrawGizmosSelected()
        {
            if (_lowEnd == null || _highEnd == null) return;
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
            Gizmos.DrawLine(_lowEnd.position, _highEnd.position);
            Gizmos.DrawWireSphere(_lowEnd.position, 0.08f);
            Gizmos.DrawWireSphere(_highEnd.position, 0.1f);
        }
    }
}
