using UnityEngine;


using Flynn.Core;
using Flynn.Player;
using Flynn.UI.Core;

namespace Flynn.Resources
{
    /// <summary>
    /// One row in a ResourceNodeConfig drop table. References the <see cref="ItemDefinition"/> to
    /// grant; the world prefab is resolved from <see cref="ItemDefinition.worldPrefab"/> by the
    /// spawner, so a single item drives both its inventory identity and its dropped appearance.
    /// </summary>
    [System.Serializable]
    public struct DropEntry
    {
        [Tooltip("Item granted by this drop. Its worldPrefab is what appears on the ground.")]
        public ItemDefinition item;

        [Tooltip("Probability (0–1) this entry produces a drop at all.")]
        [Range(0f, 1f)] public float dropChance;

        [Tooltip("Minimum number dropped when the roll succeeds.")]
        [Min(1)] public int minCount;

        [Tooltip("Maximum number dropped when the roll succeeds (inclusive).")]
        [Min(1)] public int maxCount;
    }

}
