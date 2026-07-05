using UnityEngine;


using Flynn.Player;

using Flynn.Player.Interaction;
namespace Flynn.World
{
    /// <summary>
    /// The single droppable / pickupable unit in the world: an <see cref="ItemDefinition"/> plus a
    /// stack count. Everything that can sit on the ground — resource drops, echo shards, the wrench,
    /// items dropped from the inventory — is a WorldItem. Auto-collecting items are pulled in by
    /// <see cref="DroppedItemMagnet"/>; non-auto items (the wrench) are picked up with the pickup key
    /// via <see cref="PlayerMouseAimer"/>/<see cref="PlayerPickupController"/>.
    ///
    /// Detection is by mouse raycast (solid, non-trigger collider on the pickup layer) — no registry.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class WorldItem : MonoBehaviour, IInteractionPromptProvider
    {
        [Tooltip("Item this world object represents.")]
        [SerializeField] private ItemDefinition _item;

        [Tooltip("How many of the item this stack holds.")]
        [SerializeField] private int _count = 1;

        [Header("Pile Variant")]
        [Tooltip("Sprite swapped in when the stack is at least Pile Threshold (optional).")]
        [SerializeField] private Sprite _pileSprite;
        [SerializeField] private int _pileThreshold = 3;

        public ItemDefinition Item => _item;
        public int Count => _count;

        /// <summary>Set the item + count, e.g. when spawned by <see cref="WorldItemSpawner"/>.</summary>
        public void Configure(ItemDefinition item, int count)
        {
            _item = item;
            _count = Mathf.Max(1, count);

            if (_pileSprite != null && _count >= _pileThreshold)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = _pileSprite;
            }
        }

        /// <summary>Reduce the stack after a partial collection; returns the remaining count.</summary>
        public int Reduce(int amount)
        {
            _count = Mathf.Max(0, _count - Mathf.Max(0, amount));
            return _count;
        }

        // Only key-pickup items prompt; auto-collect drops fly in on their own (no prompt).
        public bool TryGetPrompt(out InteractionPrompt prompt)
        {
            if (_item == null || _item.AutoCollects)
            {
                prompt = default;
                return false;
            }
            prompt = new InteractionPrompt("E", "Pick Up", transform, _item.displayName);
            return true;
        }
    }

}
