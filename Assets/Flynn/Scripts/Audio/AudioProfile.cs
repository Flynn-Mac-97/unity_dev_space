using UnityEngine;

/// <summary>
/// ScriptableObject describing one SFX: its clip(s) and playback settings. Assign one
/// per event slot on the AudioManager. Multiple clips = random variation per play.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Audio Profile", fileName = "AudioProfile")]
public class AudioProfile : ScriptableObject
{
    [Tooltip("Clip(s) for this sound. A random one plays each time (variation).")]
    public AudioClip[] clips;

    /// <summary>A random clip from the set, or null if none assigned.</summary>
    public AudioClip Clip => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

    [Header("Volume")]
    [Tooltip("Base volume for playback (0\u20131).")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("Pitch Variation")]
    [Tooltip("Minimum pitch offset.")]
    public float pitchMin = 0.9f;

    [Tooltip("Maximum pitch offset.")]
    public float pitchMax = 1.1f;

    [Header("Spatial")]
    [Tooltip("Maximum distance the sound can be heard.")]
    public float maxDistance = 20f;

    [Tooltip("Whether this sound uses 3D spatial blending.")]
    public bool spatialBlend = true;
}
