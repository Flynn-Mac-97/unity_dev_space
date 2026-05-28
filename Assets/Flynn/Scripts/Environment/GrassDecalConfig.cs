using System;
using UnityEngine;

/// <summary>
/// Data asset that defines what decals scatter on a grass surface and how densely.
/// Assign one asset per biome/zone and swap it on <see cref="GrassDecalPlacer"/> per island.
/// </summary>
[CreateAssetMenu(menuName = "Solarpunk/Grass Decal Config", fileName = "GrassDecalConfig")]
public class GrassDecalConfig : ScriptableObject
{
    [Serializable]
    public class DecalEntry
    {
        [Tooltip("Prefab to spawn. Should have a SpriteRenderer + Billboard component for 2.5D.")]
        public GameObject prefab;

        [Tooltip("Relative spawn weight vs other entries in the list.")]
        [Min(0.01f)]
        public float weight = 1f;
    }

    [Header("Decal Prefabs")]
    [Tooltip("Pool of prefabs to scatter. Each entry has an independent spawn weight.")]
    public DecalEntry[] entries = Array.Empty<DecalEntry>();

    [Header("Density")]
    [Tooltip("Approximate number of decals per square unit of grass surface area.")]
    [Min(0f)]
    public float densityPerUnit = 0.15f;

    [Tooltip("Maximum total decals spawned regardless of area (prevents runaway generation on huge islands).")]
    public int maxDecals = 300;

    [Header("Scale Variation")]
    public float scaleMin = 0.7f;
    public float scaleMax = 1.3f;

    [Header("Rotation")]
    [Tooltip("Randomise Y-axis rotation. Useful for flowers/rocks; disable for things with a clear facing direction.")]
    public bool randomYRotation = true;

    [Header("Height Offset")]
    [Tooltip("World-space Y offset applied to all decals so they sit on top of the grass surface.")]
    public float yOffset = 0.01f;

    [Header("Placement")]
    [Tooltip("Minimum distance between any two decals (world units). Prevents obvious clumping.")]
    [Min(0f)]
    public float minSpacing = 0.4f;

    /// <summary>
    /// Picks a random prefab from <see cref="entries"/> using weighted probability.
    /// Returns null if the entries array is empty.
    /// </summary>
    public GameObject PickRandom(System.Random rng)
    {
        if (entries == null || entries.Length == 0) return null;

        float total = 0f;
        foreach (var e in entries) total += e.weight;

        double roll = rng.NextDouble() * total;
        float  acc  = 0f;
        foreach (var e in entries)
        {
            acc += e.weight;
            if (roll <= acc) return e.prefab;
        }
        return entries[entries.Length - 1].prefab;
    }
}
