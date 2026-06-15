using UnityEngine;

/// <summary>
/// Marks a world object the player can grab and push/pull with E. The object is
/// kinematic by default so it can't be moved by physics collisions — only the
/// <see cref="PlayerCarryController"/> positions it while held. Implements
/// <see cref="IInteractionPromptProvider"/> so the HUD shows the prompt automatically.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Grabbable : MonoBehaviour, IInteractionPromptProvider
{
    [Tooltip("Display name shown in the prompt (e.g. 'Crate').")]
    [SerializeField] private string _displayName = "Crate";

    /// <summary>True while this object is being carried by the player.</summary>
    public bool IsHeld { get; private set; }

    private Rigidbody _rb;

    /// <summary>The Rigidbody on this object.</summary>
    public Rigidbody Body => _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // Always kinematic: the object is only moved by the carry controller,
        // never by physics collisions or forces.
        if (_rb != null) _rb.isKinematic = true;
    }

    /// <summary>Attach to the player — called by PlayerCarryController.</summary>
    public void OnGrab()
    {
        IsHeld = true;
    }

    /// <summary>Detach from the player — called by PlayerCarryController.</summary>
    public void OnRelease()
    {
        IsHeld = false;
    }

    public bool TryGetPrompt(out InteractionPrompt prompt)
    {
        if (IsHeld)
            prompt = new InteractionPrompt("E", "Release", transform, _displayName);
        else
            prompt = new InteractionPrompt("E", "Grab", transform, _displayName);
        return true;
    }
}
