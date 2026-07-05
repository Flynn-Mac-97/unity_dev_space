using UnityEngine;
using Flynn.Common;
using Flynn.Core;
using Flynn.Events;
using Flynn.Player;
using Flynn.Player.Interaction;
using Flynn.Transmitter;
using Flynn.UI.Core;

namespace Flynn.Tutorial
{
    /// <summary>
    /// Tutorial processing station: accepts biomass from the player's inventory
    /// and converts it into power for the linked TransmitterStation.
    /// Wire via Interactable component's OnInteract UnityEvent → TryProcess().
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ProcessingStation : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private TransmitterStation _targetStation;

        [Header("Fuel")]
        [Tooltip("Item this station accepts (e.g. Biomass).")]
        [SerializeField] private ItemDefinition _acceptedItem;
        [Tooltip("Power added to the transmitter per unit consumed.")]
        [SerializeField] private float _powerPerUnit = 8f;
        [Tooltip("Units consumed per press.")]
        [SerializeField] private int _unitsPerPress = 1;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _glow;
        [SerializeField] private float _glowFlashDuration = 0.3f;

        private float _glowTimer;

        private void Update()
        {
            if (_glowTimer > 0f)
            {
                _glowTimer -= Time.deltaTime;
                if (_glow != null)
                {
                    float a = Mathf.Clamp01(_glowTimer / _glowFlashDuration);
                    var c = _glow.color;
                    c.a = a * 0.6f;
                    _glow.color = c;
                }
            }
        }

        /// <summary>Called by Interactable.OnInteract. Consumes biomass and adds power.</summary>
        public void TryProcess()
        {
            if (_targetStation == null || _acceptedItem == null || PlayerInventory.Instance == null) return;

            var inv = PlayerInventory.Instance;
            int consumed = inv.TryConsume(_acceptedItem, _unitsPerPress);
            if (consumed <= 0) return;

            _targetStation.AddPower(_powerPerUnit * consumed);
            FlashGlow();
        }

        private void FlashGlow()
        {
            _glowTimer = _glowFlashDuration;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.5f);
        }
#endif
    }
}
