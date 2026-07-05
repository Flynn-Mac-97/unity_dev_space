using UnityEngine;

using Flynn.UI.Core;
using Flynn.Player.Interaction;

namespace Flynn.UI.Screens.InteractTag
{
    /// <summary>
    /// The missing bridge between hover detection and the interact tag. Subscribes to
    /// <see cref="MousePointer.OnHoverChanged"/>; when the hovered object (or a parent)
    /// exposes an <see cref="IInteractionPromptProvider"/> offering a valid prompt, shows
    /// the <see cref="InteractTagPanel"/> with the prompt's display text — otherwise hides it.
    ///
    /// Single responsibility: hover → prompt → tag. Holds no gameplay logic and knows no
    /// concrete interactable type (that's the whole point of the provider interface). Lives on
    /// the same GameObject as the <see cref="InteractTagPanel"/>; the panel is wired in the
    /// Inspector (no Find / FindObjectOfType).
    /// </summary>
    public class WorldInteractTagPresenter : MonoBehaviour
    {
        [Tooltip("The tag panel this presenter drives. On the same GameObject by convention.")]
        [SerializeField] private InteractTagPanel _panel;

        private MousePointer _pointer;
        private IInteractionPromptProvider _current;
        private bool _subscribed;

        private void OnEnable() => TrySubscribe();

        // MousePointer is a singleton set in Awake (execution order -90). If it wasn't ready at
        // our OnEnable, late-bind once on Start.
        private void Start() => TrySubscribe();

        private void OnDisable()
        {
            if (_subscribed && _pointer != null) _pointer.OnHoverChanged -= HandleHoverChanged;
            _subscribed = false;
            if (_panel != null) _panel.HideTag();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            _pointer = MousePointer.Instance;
            if (_pointer == null) return;
            _pointer.OnHoverChanged += HandleHoverChanged;
            _subscribed = true;
            HandleHoverChanged(_pointer.HoverObject);
        }

        private void HandleHoverChanged(GameObject hovered)
        {
            _current = hovered != null ? hovered.GetComponentInParent<IInteractionPromptProvider>() : null;
            Refresh();
        }

        // While the SAME object stays hovered, its prompt can still flip valid↔invalid (range,
        // battery, scan state). That's a continuous condition, not an event — poll it cheaply,
        // but only while a provider is actually under the cursor.
        private void Update()
        {
            if (_current != null) Refresh();
        }

        private void Refresh()
        {
            if (_panel == null) return;
            if (_current != null && _current.Equals(null))
            {
                _current = null;
                _panel.HideTag();
                return;
            }
            if (_current != null && _current.TryGetPrompt(out InteractionPrompt prompt) && prompt.IsValid)
                _panel.Show(prompt.Display);
            else
                _panel.HideTag();
        }
    }
}
