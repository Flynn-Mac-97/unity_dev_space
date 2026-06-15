using UnityEngine;
using Flynn.Events;

/// <summary>
/// 2D equivalent of <see cref="WorldInteractTagPresenter"/>. Drives the
/// <see cref="InteractTagPanel"/> from the 2D hover source (<see cref="PlayerMouseAimer2D"/>).
/// Shows interaction prompts when the player is near an interactable, hides otherwise.
/// </summary>
public class WorldInteractTagPresenter2D : MonoBehaviour
{
    [SerializeField] private PlayerMouseAimer2D _aimer;
    [SerializeField] private InteractTagPanel _tagPanel;

    private void Awake()
    {
        if (_aimer == null) _aimer = FindObjectOfType<PlayerMouseAimer2D>();
        if (_tagPanel == null) _tagPanel = FindObjectOfType<InteractTagPanel>();
    }

    private void LateUpdate()
    {
        if (_aimer == null || _tagPanel == null) return;

        var result = _aimer.HoverResult;
        IInteractionPromptProvider hovered = result.Interactable;

        if (hovered is UnityEngine.Object obj && obj == null) hovered = null;

        if (hovered != null && hovered.TryGetPrompt(out InteractionPrompt prompt) && prompt.IsValid)
        {
            _tagPanel.Show(prompt.Display);
        }
        else
        {
            _tagPanel.HideTag();
        }
    }
}
