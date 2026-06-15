using System.Collections.Generic;
using UnityEngine;
using Flynn.Events;

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

    private readonly List<AudioSource> _sources = new();
    private int _next;

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
        bus.Subscribe<RopeGrappleStarted>(OnRopeGrappleStarted);
        bus.Subscribe<PlayerJumped>(OnPlayerJumped);
        bus.Subscribe<PlayerLanded>(OnPlayerLanded);
        bus.Subscribe<TransmitterFed>(OnTransmitterFed);
        bus.Subscribe<BatteryLow>(OnBatteryLow);
        bus.Subscribe<BatteryEmpty>(OnBatteryEmpty);
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
        bus.Unsubscribe<RopeGrappleStarted>(OnRopeGrappleStarted);
        bus.Unsubscribe<PlayerJumped>(OnPlayerJumped);
        bus.Unsubscribe<PlayerLanded>(OnPlayerLanded);
        bus.Unsubscribe<TransmitterFed>(OnTransmitterFed);
        bus.Unsubscribe<BatteryLow>(OnBatteryLow);
        bus.Unsubscribe<BatteryEmpty>(OnBatteryEmpty);
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
    private void OnItemPickedUp(ItemPickedUp evt) => Play(_pickup);
    private void OnRopeGrappleStarted(RopeGrappleStarted evt) => Play(_grapple);
    private void OnPlayerJumped(PlayerJumped evt) => Play(_jump, evt.Position);
    private void OnPlayerLanded(PlayerLanded evt) => Play(_land, evt.Position);
    private void OnTransmitterFed(TransmitterFed evt) => Play(_transmitterFeed);
    private void OnBatteryLow(BatteryLow evt) => Play(_batteryLow);
    private void OnBatteryEmpty(BatteryEmpty evt) => Play(_batteryEmpty);
}
