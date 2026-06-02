using UnityEngine;

/// <summary>
/// Describes a single inventory item. Assign via Inspector; instances live in
/// Assets/Flynn/Configs/Items/. The attackAnimIndex maps to the four attack
/// clips: 1 = pick, 2 = axe, 3 = hammer, 4 = wrench.
/// </summary>
[CreateAssetMenu(menuName = "Flynn/Item Definition", fileName = "ItemDefinition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemId;
    public string displayName;
    public Sprite icon;

    [Header("Tool")]
    public ItemType itemType;

    [Tooltip("Animator AttackIndex (1-4) triggered when this tool swings. 0 = no attack.")]
    [Range(0, 4)]
    public int attackAnimIndex;
}
