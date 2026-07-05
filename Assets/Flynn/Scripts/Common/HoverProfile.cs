using UnityEngine;

namespace Flynn.Common
{
    /// <summary>
    /// Shared settings for Hoverable behaviour. Assign one profile per object type
    /// (resources, tutorial objects, NPCs, etc.) so all instances share the same
    /// scale, lerp speed, and outline material. Update here → all instances change.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Hover Profile", fileName = "HoverProfile")]
    public class HoverProfile : ScriptableObject
    {
        [Header("Outline")]
        [Tooltip("Material swapped in on hover. Must use Flynn/SpriteOutline shader.")]
        public Material outlineMaterial;

        [Header("Scale Pop")]
        [Tooltip("Scale multiplier on hover (1 = none, 1.05 = slight pop).")]
        [Range(1f, 1.3f)]
        public float hoverScale = 1.05f;

        [Tooltip("How fast the scale transitions.")]
        public float scaleLerpSpeed = 12f;
    }
}
