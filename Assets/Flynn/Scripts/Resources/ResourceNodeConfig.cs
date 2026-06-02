using UnityEngine;

/// <summary>
/// Immutable data for a harvestable resource node.
/// One asset per resource type (e.g. Tree_Config, Stone_Config, Cache_Config).
/// Assign to a <see cref="ResourceNode"/> in the Inspector.
///
/// Extending to a new resource type:
///   1. Create a new asset via Flynn/Resource/Node Config.
///   2. Set <see cref="requiredTool"/> to the matching ItemType.
///   3. Fill <see cref="drops"/> with drop-table entries.
///   4. Assign the asset to the new prefab's ResourceNode component.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Resource/Node Config", fileName = "ResourceNodeConfig")]
public class ResourceNodeConfig : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display label used in hover UI and debug.")]
    public string displayName = "Resource";

    [Header("Tool")]
    [Tooltip("ItemType that must be active (or the multitool wrench) to harvest this resource.")]
    public ItemType requiredTool = ItemType.Axe;

    [Header("Health")]
    [Tooltip("Number of damage-points before this node breaks.")]
    [Min(1)] public int maxHealth = 3;

    [Header("Drops")]
    [Tooltip("Items/prefabs to spawn when the node is depleted.")]
    public DropEntry[] drops = System.Array.Empty<DropEntry>();

    [Tooltip("Radius around the node position to scatter dropped items.")]
    [Min(0f)] public float dropScatterRadius = 0.8f;
}
