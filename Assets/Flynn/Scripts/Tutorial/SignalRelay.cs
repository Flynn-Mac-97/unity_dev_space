using UnityEngine;
using Flynn.Common;
using Flynn.Transmitter;
using Flynn.Npc;

namespace Flynn.Tutorial
{
    /// <summary>
    /// Tutorial signal relay: the final objective. Requires all linked solar collectors
    /// to be cleaned AND the transmitter to have enough power.
    /// Activate via Interactable.OnInteract → Activate().
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SignalRelay : MonoBehaviour
    {
        [Header("Requirements")]
        [SerializeField] private TransmitterStation _targetStation;
        [SerializeField] private SolarCollector[] _requiredCollectors;
        [Tooltip("Minimum transmitter power required to activate.")]
        [SerializeField] private float _requiredPower = 80f;

        [Header("Visual")]
        [SerializeField] private GameObject _activationEffect;
        [SerializeField] private AudioClip _activationSound;

        public bool IsActivated { get; private set; }

        public bool CanActivate =>
            !IsActivated &&
            _targetStation != null &&
            _targetStation.Power >= _requiredPower &&
            AllCollectorsCleaned;

        private bool AllCollectorsCleaned
        {
            get
            {
                if (_requiredCollectors == null || _requiredCollectors.Length == 0) return true;
                for (int i = 0; i < _requiredCollectors.Length; i++)
                {
                    if (_requiredCollectors[i] == null) return false;
                    if (!_requiredCollectors[i].IsCleaned) return false;
                }
                return true;
            }
        }

        /// <summary>Called by Interactable.OnInteract. Activates the relay if conditions are met.</summary>
        public void Activate()
        {
            if (IsActivated || !CanActivate) return;

            IsActivated = true;

            if (_activationEffect != null)
                _activationEffect.SetActive(true);

            if (_activationSound != null)
                AudioSource.PlayClipAtPoint(_activationSound, transform.position, 0.8f);

            var scanUI = FindFirstObjectByType<ScanUIController>();
            if (scanUI != null)
                scanUI.ShowMessage(
                    "The signal relay activates. A hum builds, rises, and reaches outward...\n\n" +
                    "Silence.\n\n" +
                    "Then — faint, crackling, impossibly far away — a response. Another island. Another voice.\n\n" +
                    "The Station's chimes ring in a chord you've never heard before.\n\n" +
                    "You have a way forward.",
                    12f);

            Debug.Log("[SignalRelay] Activated! Tutorial complete.");

            // Complete gameplay-driven objectives
            ObjectiveTracker.CompleteFromGameplay("objective.activate_relay");
            ObjectiveTracker.CompleteFromGameplay("complete.relay_activated");
        }
    }
}
