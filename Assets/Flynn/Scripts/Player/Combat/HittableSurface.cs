using UnityEngine;

/// <summary>
/// The physical material of a meleeable surface, used to decide which tool the
/// wrench multitool "transforms" into when swung at it (and so which attack
/// animation plays). Air (nothing hovered) is handled by the swing controller,
/// not represented here.
/// </summary>
public enum SurfaceMaterial
{
    Wood,
    Metal,
    Rock,
}

/// <summary>
/// Marks any object as a meleeable surface with a <see cref="SurfaceMaterial"/>.
/// Drop on crates, walls, etc. so the wrench reads them as wood/metal/rock and
/// swings the matching tool. <see cref="ResourceNode"/> does NOT need this — it
/// derives its own tool from its required-tool config; this is for non-resource
/// props that still want the tool-swap read.
///
/// AttackAnimIndex maps SurfaceMaterial → Animator AttackIndex (1-4), matching
/// <see cref="ResourceNode.AttackAnimIndex"/>: Pick = 1 | Axe = 2 | Hammer = 3 | Wrench = 4.
/// </summary>
public class HittableSurface : MonoBehaviour
{
    [Tooltip("Physical material of this surface. Decides which tool the wrench swings: Wood→Axe, Metal→Hammer, Rock→Pick.")]
    [SerializeField] private SurfaceMaterial _material = SurfaceMaterial.Wood;

    public SurfaceMaterial Material => _material;

    /// <summary>Animator AttackIndex for this surface's matching tool.</summary>
    public int AttackAnimIndex => _material switch
    {
        SurfaceMaterial.Wood  => 2, // axe
        SurfaceMaterial.Metal => 3, // hammer
        SurfaceMaterial.Rock  => 1, // pick
        _                     => 4, // wrench (shouldn't hit; defensive)
    };
}
