using UnityEngine;

/// <summary>The flavour of a rope anchor. Cosmetic / future tuning hook.</summary>
public enum AnchorType { Stub, Stone, Rock }

/// <summary>
/// Marks a world object the rope lasso can latch onto and reel the player toward.
/// Detection is by mouse raycast in <see cref="PlayerMouseAimer"/> — no registry, no
/// trigger volume. The collider must be a SOLID (non-trigger) collider on a layer in the
/// aimer's grapple mask so the ray can hit it; the lasso latches on the exact hit point.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RopeAnchor : MonoBehaviour, IInteractionPromptProvider
{
    [Tooltip("Cosmetic anchor flavour; reserved for future per-type tuning.")]
    [SerializeField] private AnchorType _type = AnchorType.Stub;

    public AnchorType Type => _type;

    public bool TryGetPrompt(out InteractionPrompt prompt)
    {
        prompt = new InteractionPrompt("Q", "Grapple", transform);
        return true;
    }
}
