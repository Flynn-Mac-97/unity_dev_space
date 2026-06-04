using UnityEngine;

/// <summary>
/// Marks a movable world object the rope lasso can reel toward the player. Detection is by
/// mouse raycast in <see cref="PlayerMouseAimer"/> — no registry, no separate detection
/// trigger. The object's solid (non-trigger) collider must sit on a layer in the aimer's
/// grapple mask so the ray can hit it. The reel applies force to this Rigidbody.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class RopePullable : MonoBehaviour, IInteractionPromptProvider
{
    public Rigidbody Body { get; private set; }

    private void Awake() => Body = GetComponent<Rigidbody>();

    public bool TryGetPrompt(out InteractionPrompt prompt)
    {
        prompt = new InteractionPrompt("Q", "Grapple", transform);
        return true;
    }
}
