using UnityEngine;
using UnityEngine.Events;
using Flynn.Common;
using Flynn.UI.Core;

namespace Flynn.Player.Interaction
{
    /// <summary>
    /// Drop-on interaction component. Configure key, range, and prompt in the Inspector.
    /// Wire OnInteract to any method (ProcessingStation.TryProcess, SolarCollector.Clean, etc).
    /// Pairs with Hoverable for outline highlight and InteractionRouter for key routing.
    /// </summary>
    public class Interactable : MonoBehaviour, IInteractionPromptProvider
    {
        [Header("Input")]
        [Tooltip("Key the InteractionRouter watches for this interactable.")]
        [SerializeField] private KeyCode _activationKey = KeyCode.E;

        [Header("Range")]
        [Tooltip("How close the player must be. 0 = no range check.")]
        [SerializeField] private float _interactionRadius = 3f;

        [Header("Prompt")]
        [SerializeField] private string _promptVerb = "Interact";
        [SerializeField] private string _promptLabel = "";

        [Header("Player")]
        [Tooltip("SO handle to the player's transform. Required for range checks.")]
        [SerializeField] private PlayerAnchor _playerAnchor;

        [Header("Events")]
        [Tooltip("Fired when the activation key is pressed while hovered and in range.")]
        [SerializeField] private UnityEvent _onInteract = new UnityEvent();

        /// <summary>Key the InteractionRouter should watch.</summary>
        public KeyCode ActivationKey => _activationKey;

        /// <summary>True when the player is within interaction range (or no range check).</summary>
        public bool IsPlayerInRange
        {
            get
            {
                if (_interactionRadius <= 0f) return true;
                if (_playerAnchor == null || !_playerAnchor.HasPlayer) return false;
                return Vector2.Distance(_playerAnchor.Current.position, transform.position) <= _interactionRadius;
            }
        }

        /// <summary>Called by InteractionRouter when the activation key is pressed while this object is hovered.</summary>
        public void Interact()
        {
            if (!IsPlayerInRange) return;
            _onInteract?.Invoke();
        }

        public bool TryGetPrompt(out InteractionPrompt prompt)
        {
            if (!IsPlayerInRange)
            {
                prompt = default;
                return false;
            }
            prompt = new InteractionPrompt(_activationKey.ToString(), _promptVerb, transform, _promptLabel);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_interactionRadius > 0f)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.4f);
                Gizmos.DrawWireSphere(transform.position, _interactionRadius);
            }
        }
#endif
    }
}
