using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single scene driver for <see cref="WorldInteractTag"/>. Reads the one hover source
/// (<see cref="PlayerMouseAimer.HoveredInteractable"/>) each frame and shows the hovered
/// interactable's tag with its prompt text, hiding the previously-hovered one. Because hover is
/// resolved once in the aimer, exactly one tag is visible at a time and no tag runs its own raycast.
///
/// Drop one of these in the scene (e.g. on the player or a UI/Managers object) and assign the aimer.
/// </summary>
public class WorldInteractTagPresenter : MonoBehaviour
{
    [SerializeField] private PlayerMouseAimer _aimer; // falls back to a one-time scene find

    // Provider → its tag (or null if it has none). Cached so we GetComponent once per interactable.
    private readonly Dictionary<IInteractionPromptProvider, WorldInteractTag> _tags = new();
    private WorldInteractTag _shown;

    private void Awake()
    {
        if (_aimer == null) _aimer = FindObjectOfType<PlayerMouseAimer>();
    }

    private void LateUpdate()
    {
        if (_aimer == null) return;

        IInteractionPromptProvider hovered = _aimer.HoveredInteractable;
        WorldInteractTag tag = hovered != null && hovered.TryGetPrompt(out InteractionPrompt prompt) && prompt.IsValid
            ? Resolve(hovered)
            : null;

        if (tag == _shown)
        {
            // Same target still hovered — refresh text in case the prompt changed (e.g. count).
            if (tag != null && hovered.TryGetPrompt(out InteractionPrompt p)) tag.Show(p.Display);
            return;
        }

        if (_shown != null) _shown.Hide();
        if (tag != null && hovered.TryGetPrompt(out InteractionPrompt cur)) tag.Show(cur.Display);
        _shown = tag;
    }

    /// <summary>Find (and cache) the tag on a hovered provider's GameObject or its children.</summary>
    private WorldInteractTag Resolve(IInteractionPromptProvider provider)
    {
        if (_tags.TryGetValue(provider, out WorldInteractTag cached)) return cached;

        WorldInteractTag tag = provider is Component c ? c.GetComponentInChildren<WorldInteractTag>(true) : null;
        _tags[provider] = tag;
        return tag;
    }
}
