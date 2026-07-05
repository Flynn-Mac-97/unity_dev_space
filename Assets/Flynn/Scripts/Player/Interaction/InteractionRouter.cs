using UnityEngine;
using Flynn.UI.Core;
using Flynn.Npc;

namespace Flynn.Player.Interaction
{
    /// <summary>
    /// Sits on the Player. Each frame: checks what the cursor is hovering, finds
    /// Interactable on the hovered GO (walking up the hierarchy), and routes the
    /// activation key press to it. Objects without Interactable (scan targets, resource
    /// nodes) keep their own input handling.
    /// </summary>
    public class InteractionRouter : MonoBehaviour
    {
        private Interactable _hovered;

        private void Update()
        {
            if (DialogueManager.IsDialogueOpen) { _hovered = null; return; }

            ResolveHovered();

            if (_hovered != null && Input.GetKeyDown(_hovered.ActivationKey))
                _hovered.Interact();
        }

        private void ResolveHovered()
        {
            var mp = MousePointer.Instance;
            if (mp == null || mp.HoverObject == null)
            {
                _hovered = null;
                return;
            }

            // Walk up the hierarchy to find an Interactable component
            var go = mp.HoverObject;
            while (go != null)
            {
                var found = go.GetComponent<Interactable>();
                if (found != null) { _hovered = found; return; }
                go = go.transform.parent?.gameObject;
            }

            _hovered = null;
        }
    }
}
