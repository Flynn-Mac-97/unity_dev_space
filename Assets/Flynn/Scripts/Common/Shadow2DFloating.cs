using UnityEngine;

/// <summary>
/// Add alongside Shadow2DTarget on objects that float or overhang.
/// Makes the shadow softer, more stretched, and larger based on
/// how far the object is from the ground — mimicking real light diffusion.
/// </summary>
[ExecuteInEditMode]
public class Shadow2DFloating : MonoBehaviour
{
    [Tooltip("How far the object floats above the ground in world units. " +
             "Drives lift, opacity fade, stretch increase, and spread scale.")]
    [SerializeField, Range(0f, 10f)] private float _floatHeight = 1f;

    [Tooltip("How much shadow opacity decreases per unit of float height. " +
             "0 = no fade; 0.15 = 15% less opaque per unit.")]
    [SerializeField, Range(0f, 0.5f)] private float _opacityFalloff = 0.12f;

    [Tooltip("Extra shadow stretch added per unit of float height. " +
             "Floating objects cast longer, more diffuse shadows.")]
    [SerializeField, Range(0f, 1f)] private float _stretchPerUnit = 0.25f;

    [Tooltip("Shadow sprite scale increase per unit of float height. " +
             "Simulates the shadow spreading out as it gets further from the ground.")]
    [SerializeField, Range(0f, 0.5f)] private float _spreadPerUnit = 0.08f;

    /// <summary>World-unit distance the object floats above the ground.</summary>
    public float FloatHeight => _floatHeight;

    /// <summary>Opacity multiplier accounting for float distance (0–1).</summary>
    public float OpacityMultiplier => Mathf.Clamp01(1f - _floatHeight * _opacityFalloff);

    /// <summary>Extra stretch to add to the shadow based on float height.</summary>
    public float ExtraStretch => _floatHeight * _stretchPerUnit;

    /// <summary>Extra scale multiplier to spread the shadow based on float height.</summary>
    public float ExtraSpreadScale => 1f + _floatHeight * _spreadPerUnit;
}
