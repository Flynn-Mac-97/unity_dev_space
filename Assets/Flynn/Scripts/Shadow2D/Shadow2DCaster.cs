using UnityEngine;

namespace Flynn.Shadow2D
{
    /// <summary>
    /// Marker component for sprites that should cast a ProjectedShadow2D.
    /// Add to any GameObject with a SpriteRenderer. Shadow2DManager will
    /// create and manage a shadow sprite child automatically.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class Shadow2DCaster : MonoBehaviour
    {
        [Tooltip("Multiplier on the manager's shadow stretch. 1 = default, 0 = no shadow.")]
        [SerializeField] private float _stretchMultiplier = 1f;

        /// <summary>Per-caster stretch multiplier applied on top of the manager's base stretch.</summary>
        public float StretchMultiplier => _stretchMultiplier;
    }
}
