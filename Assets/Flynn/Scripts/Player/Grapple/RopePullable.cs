using UnityEngine;

/// <summary>
/// Marks a movable world object the rope lasso can reel toward the player. Detection is a
/// cursor raycast in <see cref="RopeLassoController"/> — no registry, no separate detection
/// trigger. The object's Collider2D must sit on a layer in the lasso's grapple mask so the
/// cursor can hit it. The reel applies velocity to this Rigidbody2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class RopePullable : MonoBehaviour, IInteractionPromptProvider
{
    public Rigidbody2D Body { get; private set; }

    private void Awake() => Body = GetComponent<Rigidbody2D>();

    public bool TryGetPrompt(out InteractionPrompt prompt)
    {
        prompt = new InteractionPrompt("Q", "Grapple", transform);
        return true;
    }
}
