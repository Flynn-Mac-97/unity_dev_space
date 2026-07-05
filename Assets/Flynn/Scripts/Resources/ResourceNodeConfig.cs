using UnityEngine;


using Flynn.Core;
using Flynn.Player;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.Resources
{
    /// <summary>
    /// Immutable data for a harvestable resource node.
    /// One asset per resource type (e.g. Tree_Config, Stone_Config, Cache_Config).
    /// Assign to a <see cref="ResourceNode"/> in the Inspector.
    ///
    /// Extending to a new resource type:
    ///   1. Create a new asset via Flynn/Resource/Node Config.
    ///   2. Set <see cref="requiredTool"/> to the matching ToolType.
    ///   3. Fill <see cref="drops"/> with drop-table entries.
    ///   4. Assign the asset to the new prefab's ResourceNode component.
    /// </summary>
    [CreateAssetMenu(menuName = "Flynn/Resource/Node Config", fileName = "ResourceNodeConfig")]
    public class ResourceNodeConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display label used in hover UI and debug.")]
        public string displayName = "Resource";

        [Header("Type")]
        [Tooltip("What kind of resource this is. Used by ToolEffectivenessTable for damage multiplier lookup.")]
        public ResourceType resourceType = ResourceType.Stone;

        [Header("Tool")]
        [Tooltip("ToolType that must be active (or the multitool wrench) to harvest this resource.")]
        public ToolType requiredTool = ToolType.Axe;

        [Header("Health")]
        [Tooltip("Number of damage-points before this node breaks.")]
        [Min(1)] public int maxHealth = 3;

        [Header("Visuals")]
        [Tooltip("Default sprite shown at full health. Leave null for flora (sprite set by tilemap at runtime).")]
        public Sprite sprite;

        [Tooltip("Optional damage stages — sprites shown as health drops. Leave empty for single-sprite resources.")]
        public ResourceStage[] healthStages;

        [Header("Drops")]
        [Tooltip("Items/prefabs to spawn when the node is depleted.")]
        public DropEntry[] drops = System.Array.Empty<DropEntry>();

        [Tooltip("Radius around the node position to scatter dropped items.")]
        [Min(0f)] public float dropScatterRadius = 0.8f;

        [Header("Physics")]
        [Tooltip("Resistance to being shaken when hit. Higher = less shake (e.g. Tree=100, Bush=10).")]
        [Min(1f)] public float weight = 10f;

        [Tooltip("If true, this resource sways with wind. Disable for rocks, crystals, etc.")]
        public bool swaysInWind = true;

        [Header("Hit Feedback")]
        [Tooltip("Knockback force applied to the player on hit. Heavier resources knock back more.")]
        public float hitKnockback = 0.8f;

        [Tooltip("Camera shake intensity on hit (0 = no shake).")]
        [Range(0f, 1f)] public float cameraShakeAmount = 0.3f;

        [Header("Echo Shards")]
        [Tooltip("Chance (0–1) that each hit also drops an echo shard.")]
        [Range(0f, 1f)] public float echoShardChancePerHit = 0.15f;

        [Tooltip("The echo-shard currency item dropped on a successful roll. Leave empty to disable.")]
        public ItemDefinition echoShardItem;
    }

    /// <summary>
    /// One entry in a resource's health-stage sprite table.
    /// The stage with the highest threshold that the current health fraction
    /// is still at or above is the one shown.
    /// </summary>
    [System.Serializable]
    public struct ResourceStage
    {
        [Tooltip("Health fraction at/above which this sprite is shown (0–1). 1.0 = full health.")]
        [Range(0f, 1f)] public float healthThreshold;

        [Tooltip("Sprite to display when health is at or above this threshold.")]
        public Sprite sprite;
    }
}
