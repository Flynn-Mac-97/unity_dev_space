using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Flynn.Npc;

namespace Flynn.Tutorial
{
    /// <summary>
    /// Routes dialogue-fired signals into VISIBLE world reactions, so words move
    /// the world while the dialogue panel is still open (movement is locked during
    /// dialogue but time runs — pulses play in frame).
    /// Each signal pings its scene objects: staggered scale-pop + whoosh ring + chime.
    /// Targets resolve by name at Awake; override via the serialized lists.
    /// </summary>
    public class TutorialSignalHandler : MonoBehaviour
    {
        [SerializeField] private DialogueTriggerChannel _triggerChannel;

        [Header("Pulse feel")]
        [SerializeField] private float _pulseScale = 1.18f;
        [SerializeField] private float _pulseDuration = 0.35f;
        [SerializeField] private float _staggerDelay = 0.25f;

        [Header("Targets (auto-found by name when empty)")]
        [SerializeField] private GameObject[] _feedTargets;      // processing stations
        [SerializeField] private GameObject[] _solarTargets;     // solar collectors
        [SerializeField] private GameObject[] _scanTargets;      // weather station
        [SerializeField] private GameObject[] _relayTargets;     // signal relay + transmitter

        private AudioSource _chime;

        private void Awake()
        {
            if (_feedTargets == null || _feedTargets.Length == 0)
                _feedTargets = FindByNames("ProcessingStation_1", "ProcessingStation_2");
            if (_solarTargets == null || _solarTargets.Length == 0)
                _solarTargets = FindByNames("SolarCollector_1", "SolarCollector_2", "SolarCollector_3");
            if (_scanTargets == null || _scanTargets.Length == 0)
                _scanTargets = FindByNames("WeatherStation");
            if (_relayTargets == null || _relayTargets.Length == 0)
                _relayTargets = FindByNames("SignalRelay", "ion_TransmitterStation");

            _chime = gameObject.AddComponent<AudioSource>();
            _chime.playOnAwake = false;
            _chime.spatialBlend = 0f;
            _chime.volume = 0.3f;
        }

        private void OnEnable()
        {
            if (_triggerChannel != null)
                _triggerChannel.OnRaised += HandleTrigger;
        }

        private void OnDisable()
        {
            if (_triggerChannel != null)
                _triggerChannel.OnRaised -= HandleTrigger;
        }

        private void HandleTrigger(DialogueTriggerPayload payload)
        {
            if (string.IsNullOrEmpty(payload.triggerKey)) return;

            switch (payload.triggerKey)
            {
                case "tutorial.first_feed":
                    StartCoroutine(PulseChain(_feedTargets, big: false));
                    break;
                case "tutorial.first_solar":
                    StartCoroutine(PulseChain(_solarTargets, big: false));
                    break;
                case "tutorial.scan_weather":
                    StartCoroutine(PulseChain(_scanTargets, big: false));
                    break;
                case "complete.relay_activated":
                    StartCoroutine(PulseChain(_relayTargets, big: true));
                    break;
            }
        }

        /// <summary>Staggered attention pulses across the targets — reads as the NPC
        /// pointing at things in the world, one after another.</summary>
        private IEnumerator PulseChain(GameObject[] targets, bool big)
        {
            if (targets == null) yield break;

            bool first = true;
            foreach (var go in targets)
            {
                if (go == null) continue;
                if (!first) yield return new WaitForSeconds(_staggerDelay);
                first = false;

                StartCoroutine(PulseOne(go, big));
                Chime(big);
            }

            if (big && Flynn.Effects.CameraShake.Instance != null)
                Flynn.Effects.CameraShake.Instance.Shake(0.2f, 0.3f);
        }

        private IEnumerator PulseOne(GameObject go, bool big)
        {
            if (Flynn.Effects.HitImpactFX.Instance != null)
                Flynn.Effects.HitImpactFX.Instance.PlayAirWhoosh(go.transform.position, Vector2.up);

            Transform t = go.transform;
            Vector3 baseScale = t.localScale;
            float peak = big ? _pulseScale * 1.15f : _pulseScale;
            float dur = big ? _pulseDuration * 1.4f : _pulseDuration;

            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float p = Mathf.Clamp01(e / dur);
                // fast out, springy back
                float s = 1f + (peak - 1f) * Mathf.Sin(p * Mathf.PI);
                t.localScale = baseScale * s;
                yield return null;
            }
            t.localScale = baseScale;
        }

        private void Chime(bool big)
        {
            if (_chime == null) return;
            _chime.clip = big ? Flynn.Managers.ProceduralSfx.PerfectDing : Flynn.Managers.ProceduralSfx.Catch;
            _chime.pitch = Random.Range(0.96f, 1.06f);
            _chime.Play();
        }

        private static GameObject[] FindByNames(params string[] names)
        {
            var list = new List<GameObject>();
            foreach (string n in names)
            {
                var go = GameObject.Find(n);
                if (go != null) list.Add(go);
            }
            return list.ToArray();
        }
    }
}
