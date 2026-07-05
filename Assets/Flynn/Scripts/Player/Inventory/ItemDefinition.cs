using UnityEngine;


using Flynn.Common;
using Flynn.Core;
using Flynn.World;

namespace Flynn.Player
{
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
        public ToolType itemType;

        [Tooltip("Animator AttackIndex (1-4) triggered when this tool swings. 0 = no attack.")]
        [Range(0, 4)]
        public int attackAnimIndex;

        [Header("Stacking")]
        [Tooltip("Maximum count of this item in one inventory slot. Tools (the wrench) should be 1.")]
        [Min(1)] public int maxStack = 3;

        [Header("World pickup")]
        [Tooltip("Prefab spawned to represent this item lying in / dropped into the world (carries a WorldItem). " +
                 "Used by resource drops, the drop key, and drag-to-drop.")]
        public GameObject worldPrefab;

        [Tooltip("If true, world drops of this item fly to the player automatically once there's room. " +
                 "If false (e.g. the wrench) the player must aim at it and press the pickup key.")]
        public bool autoCollect = true;

        [Header("Currency")]
        [Tooltip("If true this item is a currency: collecting it adds to currencyTarget instead of taking an inventory slot.")]
        public bool isCurrency = false;

        [Tooltip("Currency counter incremented when this item is collected. Only used when isCurrency is true.")]
        public IntVariable currencyTarget;

        /// <summary>True when this item should magnet to the player on drop (resources, currency).</summary>
        public bool AutoCollects => autoCollect || isCurrency;
    }

}
