using UnityEngine;

/// <summary>
/// One row in a ResourceNodeConfig drop table.
/// </summary>
[System.Serializable]
public struct DropEntry
{
    [Tooltip("Prefab instantiated when this drop fires. Assign a WorldItemPickup prefab or any placeholder.")]
    public GameObject prefab;

    [Tooltip("Probability (0–1) this entry produces a drop at all.")]
    [Range(0f, 1f)] public float dropChance;

    [Tooltip("Minimum number of instances spawned when the roll succeeds.")]
    [Min(1)] public int minCount;

    [Tooltip("Maximum number of instances spawned when the roll succeeds (inclusive).")]
    [Min(1)] public int maxCount;
}
