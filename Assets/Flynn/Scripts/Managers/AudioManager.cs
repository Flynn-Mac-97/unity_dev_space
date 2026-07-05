using System.Collections.Generic;
using UnityEngine;
using Flynn.Events;


using Flynn.Core;
using Flynn.Audio;
using Flynn.UI.Core;

namespace Flynn.Managers
{
    /// <summary>
    /// Scene-level SFX manager. Owns a pool of AudioSources (one per child object so each
    /// can play positionally) and plays a designer-assigned <see cref="AudioProfile"/> in
    /// response to gameplay events on the <see cref="GameEventBus"/>. Pure subscriber —
    /// no system calls it directly. One per scene on the MANAGERS object.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Pool")]
        [Tooltip("Number of pooled AudioSources for overlapping one-shots.")]
        [SerializeField] private int _poolSize = 6;

        [Header("SFX profiles (assign clips per event)")]
        [SerializeField] private AudioProfile _swing;
        [SerializeField] private AudioProfile _hit;
        [SerializeField] private AudioProfile _break;
        [SerializeField] private AudioProfile _pickup;
        [SerializeField] private AudioProfile _grapple;
        [SerializeField] private AudioProfile _jump;
        [SerializeField] private AudioProfile _land;
        [SerializeField] private AudioProfile _transmitterFeed;
        [SerializeField] private AudioProfile _batteryLow;
        [SerializeField] private AudioProfile _batteryEmpty;

        [Header("Wrench charge/throw (procedural fallback when no clip assigned)")]
        [SerializeField] private AudioProfile _chargeTick;
        [SerializeField] private AudioProfile _perfectDing;
        [SerializeField] private AudioProfile _throw;
        [SerializeField] private AudioProfile _wrenchReturn;

        private readonly List<AudioSource> _sources = new();
        private int _next;

        // Charge tick cadence state
        private int _lastChargeStep = -1;
        private bool _wasInZone;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"SFXSource_{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                _sources.Add(src);
            }
        }

        private void OnEnable()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Subscribe<ResourceDamaged>(OnResourceDamaged);
            bus.Subscribe<ResourceDepleted>(OnResourceDepleted);
            bus.Subscribe<ToolSwingStarted>(OnToolSwingStarted);
            bus.Subscribe<ToolHitTarget>(OnToolHitTarget);
            bus.Subscribe<ItemPickedUp>(OnItemPickedUp);
            bus.Subscribe<PlayerJumped>(OnPlayerJumped);
            bus.Subscribe<PlayerLanded>(OnPlayerLanded);
            bus.Subscribe<TransmitterFed>(OnTransmitterFed);
            bus.Subscribe<BatteryLow>(OnBatteryLow);
            bus.Subscribe<BatteryEmpty>(OnBatteryEmpty);
            bus.Subscribe<PowerBuildupStarted>(OnBuildupStarted);
            bus.Subscribe<PowerBuildupChanged>(OnBuildupChanged);
            bus.Subscribe<PowerBuildupReleased>(OnBuildupReleased);
            bus.Subscribe<ToolThrowReleased>(OnThrowReleased);
            bus.Subscribe<ToolReturned>(OnToolReturned);
        }

        private void OnDisable()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;
            bus.Unsubscribe<ResourceDamaged>(OnResourceDamaged);
            bus.Unsubscribe<ResourceDepleted>(OnResourceDepleted);
            bus.Unsubscribe<ToolSwingStarted>(OnToolSwingStarted);
            bus.Unsubscribe<ToolHitTarget>(OnToolHitTarget);
            bus.Unsubscribe<ItemPickedUp>(OnItemPickedUp);
            bus.Unsubscribe<PlayerJumped>(OnPlayerJumped);
            bus.Unsubscribe<PlayerLanded>(OnPlayerLanded);
            bus.Unsubscribe<TransmitterFed>(OnTransmitterFed);
            bus.Unsubscribe<BatteryLow>(OnBatteryLow);
            bus.Unsubscribe<BatteryEmpty>(OnBatteryEmpty);
            bus.Unsubscribe<PowerBuildupStarted>(OnBuildupStarted);
            bus.Unsubscribe<PowerBuildupChanged>(OnBuildupChanged);
            bus.Unsubscribe<PowerBuildupReleased>(OnBuildupReleased);
            bus.Unsubscribe<ToolThrowReleased>(OnThrowReleased);
            bus.Unsubscribe<ToolReturned>(OnToolReturned);
        }

        // ── Playback ──────────────────────────────────────────────────────────────

        private void Play(AudioProfile profile, Vector3 position)
        {
            if (profile == null) return;
            AudioClip clip = profile.Clip;
            if (clip == null) return;

            AudioSource src = _sources[_next];
            _next = (_next + 1) % _sources.Count;

            src.transform.position = position;
            src.clip = clip;
            src.volume = profile.volume;
            src.pitch = Random.Range(profile.pitchMin, profile.pitchMax);
            src.spatialBlend = profile.spatialBlend ? 1f : 0f;
            src.maxDistance = profile.maxDistance;
            src.Play();
        }

        /// <summary>Non-positional (UI) one-shot.</summary>
        private void Play(AudioProfile profile) => Play(profile, transform.position);

        // ── Event handlers ──────────────────────────────────────────────────────

        private void OnResourceDamaged(ResourceDamaged evt) => Play(_hit, evt.Position);
        private void OnResourceDepleted(ResourceDepleted evt) => Play(_break, evt.Position);
        private void OnToolSwingStarted(ToolSwingStarted evt) => Play(_swing, evt.AimPoint);
        private void OnToolHitTarget(ToolHitTarget evt) => Play(_hit, evt.Hit.HitPoint);
        private void OnItemPickedUp(ItemPickedUp evt)
        {
            // Rising collect pitch: chained pickups within the combo window step
            // the pitch up a semitone each (resets after a gap) — Stardew-style.
            _pickupCombo = Time.unscaledTime - _lastPickupTime <= PickupComboWindow
                ? Mathf.Min(_pickupCombo + 1, 8)
                : 0;
            _lastPickupTime = Time.unscaledTime;

            float pitch = Mathf.Pow(1.059463f, _pickupCombo);
            PlayPitched(_pickup, ProceduralSfx.Pop, pitch);
        }

        private const float PickupComboWindow = 1.5f;
        private int _pickupCombo;
        private float _lastPickupTime;
        private void OnPlayerJumped(PlayerJumped evt) => Play(_jump, evt.Position);
        private void OnPlayerLanded(PlayerLanded evt) => Play(_land, evt.Position);
        private void OnTransmitterFed(TransmitterFed evt) => Play(_transmitterFeed);
        private void OnBatteryLow(BatteryLow evt) => Play(_batteryLow);
        private void OnBatteryEmpty(BatteryEmpty evt) => Play(_batteryEmpty);

        // ── Wrench charge/throw ──────────────────────────────────────────────────

        private void OnBuildupStarted(PowerBuildupStarted evt)
        {
            _lastChargeStep = -1;
            _wasInZone = false;
        }

        private void OnBuildupChanged(PowerBuildupChanged evt)
        {
            // Rising tick every 1/8th of the bar; brighter tick on sweetspot entry.
            int step = Mathf.FloorToInt(evt.Normalized * 8f);
            if (step > _lastChargeStep && evt.Normalized > 0f)
            {
                _lastChargeStep = step;
                float pitch = 0.9f + 0.45f * evt.Normalized;
                PlayOrFallback(_chargeTick, ProceduralSfx.Tick, pitch);
            }
            if (evt.InPerfectZone && !_wasInZone)
                PlayOrFallback(_chargeTick, ProceduralSfx.ZoneEnter, 1.4f);
            _wasInZone = evt.InPerfectZone;
        }

        private void OnBuildupReleased(PowerBuildupReleased evt)
        {
            if (evt.IsPerfect)
                PlayOrFallback(_perfectDing, ProceduralSfx.PerfectDing, 1f);
        }

        private void OnThrowReleased(ToolThrowReleased evt)
            => PlayOrFallback(_throw, ProceduralSfx.Whoosh, Random.Range(0.95f, 1.05f));

        private void OnToolReturned(ToolReturned evt)
            => PlayOrFallback(_wrenchReturn, ProceduralSfx.Catch, 1f);

        /// <summary>Like PlayOrFallback, but the pitch multiplier also applies when the
        /// profile has a real clip (needed for the pickup combo ramp).</summary>
        private void PlayPitched(AudioProfile profile, AudioClip fallback, float pitchMul)
        {
            if (profile != null && profile.Clip != null)
            {
                AudioSource s = _sources[_next];
                _next = (_next + 1) % _sources.Count;
                s.transform.position = transform.position;
                s.clip = profile.Clip;
                s.volume = profile.volume;
                s.pitch = Random.Range(profile.pitchMin, profile.pitchMax) * pitchMul;
                s.spatialBlend = 0f;
                s.Play();
                return;
            }
            PlayOrFallback(null, fallback, pitchMul);
        }

        /// <summary>Play the designer profile when it has a clip; otherwise a generated blip
        /// so the mechanic is audible before audio assets land.</summary>
        private void PlayOrFallback(AudioProfile profile, AudioClip fallback, float pitch)
        {
            if (profile != null && profile.Clip != null) { Play(profile); return; }
            if (fallback == null) return;

            AudioSource src = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            src.transform.position = transform.position;
            src.clip = fallback;
            src.volume = 0.3f;
            src.pitch = pitch;
            src.spatialBlend = 0f;
            src.Play();
        }
    }

    /// <summary>
    /// Tiny code-generated one-shots for the wrench minigame. Placeholder juice until
    /// real clips are assigned on the AudioManager profiles.
    /// </summary>
    internal static class ProceduralSfx
    {
        private const int Rate = 44100;

        private static AudioClip _tick, _zoneEnter, _ding, _whoosh, _catch, _pop;

        public static AudioClip Tick => _tick ??= Tone("sfx_tick", 2100f, 0.035f, square: true);
        public static AudioClip Pop => _pop ??= Chime("sfx_pop", 880f, 1760f, 0.07f);
        public static AudioClip ZoneEnter => _zoneEnter ??= Tone("sfx_zone", 2800f, 0.05f, square: true);
        public static AudioClip PerfectDing => _ding ??= Chime("sfx_perfect", 1320f, 1980f, 0.18f);
        public static AudioClip Whoosh => _whoosh ??= Noise("sfx_whoosh", 0.14f);
        public static AudioClip Catch => _catch ??= Chime("sfx_catch", 880f, 1320f, 0.09f);

        private static AudioClip Tone(string name, float freq, float dur, bool square)
        {
            int n = (int)(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = 1f - (float)i / n;
                float s = Mathf.Sin(2f * Mathf.PI * freq * t);
                if (square) s = Mathf.Sign(s) * 0.6f;
                data[i] = s * env * env * 0.8f;
            }
            return Make(name, data);
        }

        private static AudioClip Chime(string name, float f1, float f2, float dur)
        {
            int n = (int)(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = 1f - (float)i / n;
                data[i] = (Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.6f
                         + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.4f) * env * env;
            }
            return Make(name, data);
        }

        private static AudioClip Noise(string name, float dur)
        {
            int n = (int)(Rate * dur);
            var data = new float[n];
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Sin(Mathf.PI * i / n); // swell in and out
                // Cheap lowpass over white noise = airy whoosh
                last = Mathf.Lerp(last, Random.Range(-1f, 1f), 0.25f);
                data[i] = last * env * 0.7f;
            }
            return Make(name, data);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
