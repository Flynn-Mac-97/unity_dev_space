using UnityEngine;
using Flynn.Player;

namespace Flynn.Pod
{
    /// <summary>
    /// The pod's burner: feeds wood from the player's inventory into the pod's
    /// power pool. Wire an Interactable's OnInteract to <see cref="TryBurn"/>.
    /// Mirrors Tutorial.ProcessingStation but targets PodPower, not the transmitter.
    /// </summary>
    public class BurnerStation : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private PodPower _pod;

        [Header("Fuel")]
        [Tooltip("Item this burner accepts (Wood).")]
        [SerializeField] private ItemDefinition _acceptedItem;
        [Tooltip("Pod power added per unit burned.")]
        [SerializeField] private float _powerPerUnit = 12f;
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
                    var c = _glow.color;
                    c.a = Mathf.Clamp01(_glowTimer / _glowFlashDuration) * 0.6f;
                    _glow.color = c;
                }
            }
        }

        /// <summary>Called by Interactable.OnInteract. Consumes wood, adds pod power.</summary>
        public void TryBurn()
        {
            if (_pod == null || _acceptedItem == null || PlayerInventory.Instance == null) return;

            int consumed = PlayerInventory.Instance.TryConsume(_acceptedItem, _unitsPerPress);
            if (consumed <= 0) return;

            _pod.AddPower(_powerPerUnit * consumed);
            _glowTimer = _glowFlashDuration;
        }
    }
}
