using UnityEngine;
using Flynn.Common;
using Flynn.Events;
using Flynn.Transmitter;
using Flynn.Npc;

namespace Flynn.Tutorial
{
    /// <summary>
    /// Tutorial solar collector: starts dirty and dim. When cleaned via Interactable's
    /// OnInteract event, provides steady power to the linked TransmitterStation.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SolarCollector : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private TransmitterStation _targetStation;

        [Header("Power")]
        [Tooltip("Power added per second to the transmitter when cleaned.")]
        [SerializeField] private float _steadyPowerPerSecond = 1.5f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _panelRenderer;
        [SerializeField] private Color _dirtyColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _cleanColor = new Color(1f, 0.95f, 0.6f, 1f);
        [Tooltip("Audio clip played when cleaned. Optional.")]
        [SerializeField] private AudioClip _cleanSound;

        public bool IsCleaned { get; private set; }

        private void Start()
        {
            ApplyVisual();
        }

        private void Update()
        {
            if (IsCleaned && _targetStation != null)
            {
                _targetStation.AddPower(_steadyPowerPerSecond * Time.deltaTime);
            }
        }

        /// <summary>Called by Interactable.OnInteract. Cleans the panel and starts power flow.</summary>
        public void Clean()
        {
            if (IsCleaned) return;
            IsCleaned = true;
            ApplyVisual();

            if (_cleanSound != null)
                AudioSource.PlayClipAtPoint(_cleanSound, transform.position, 0.5f);

            Debug.Log("[SolarCollector] Cleaned! Steady power flowing.");

            CheckAllCollectorsCleaned();
        }

        private void CheckAllCollectorsCleaned()
        {
            var all = FindObjectsByType<SolarCollector>(FindObjectsSortMode.None);
            foreach (var sc in all)
            {
                if (!sc.IsCleaned) return;
            }
            // All collectors cleaned — complete the objective
            ObjectiveTracker.CompleteFromGameplay("objective.restore_steady_power");
            Debug.Log("[SolarCollector] All solar collectors cleaned — objective completed.");
        }

        private void ApplyVisual()
        {
            if (_panelRenderer != null)
                _panelRenderer.color = IsCleaned ? _cleanColor : _dirtyColor;
        }
    }
}
