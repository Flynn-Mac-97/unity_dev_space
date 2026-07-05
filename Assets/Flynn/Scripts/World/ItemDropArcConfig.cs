using UnityEngine;

namespace Flynn.World
{
    /// <summary>
    /// Shared configuration for item drop arc motion. One asset can be assigned
    /// across all ItemDropArc instances to keep drop flight consistent.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Item Drop Arc Config", fileName = "ItemDropArcConfig")]
    public class ItemDropArcConfig : ScriptableObject
    {
        [Tooltip("Total flight time in seconds.")]
        public float duration = 1.5f;

        [Tooltip("Peak height of the arc in world units.")]
        public float arcHeight = 0.3f;

        [Tooltip("Multiplier applied to the prefab's existing scale.")]
        public float scaleMultiplier = 0.5f;

        [Tooltip("Horizontal progress curve over time (0→1). Default is ease-out cubic.")]
        public AnimationCurve horizontalCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0, 0),
            new Keyframe(1f, 1f, 0, 0));

        [Tooltip("Vertical height curve over time (0→1). Default is a parabola peaking at mid-flight.")]
        public AnimationCurve verticalCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0, 0),
            new Keyframe(0.5f, 1f, 0, 0),
            new Keyframe(1f, 0f, 0, 0));
    }
}
