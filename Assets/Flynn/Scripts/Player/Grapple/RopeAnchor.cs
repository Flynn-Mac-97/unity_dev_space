using UnityEngine;

/// <summary>The flavour of a rope anchor. Cosmetic / future tuning hook.</summary>
public enum AnchorType { Stub, Stone, Rock }

/// <summary>
/// Marks a world object the rope lasso can latch onto and pull the player toward
/// (horizontally, on the same plane). Detection is a cursor raycast in
/// <see cref="RopeLassoController"/> — no registry, no trigger volume. The Collider2D
/// must sit on a layer in the lasso's grapple mask so the cursor can hit it.
/// </summary>
[RequireComponent(typeof(Collider2D))]
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
