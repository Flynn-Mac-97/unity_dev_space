using UnityEngine;
using Flynn.Events;


using Flynn.Core;
using Flynn.Common;
using Flynn.Player;
using Flynn.UI.Core;

namespace Flynn.World
{
    /// <summary>
    /// Proximity pickup for dropped <see cref="WorldItem"/>s. Drops fall and settle on the
    /// ground via Rigidbody2D physics. When the player walks within pickup radius,
    /// the item is collected into inventory or currency counter.
    /// Non-auto items (wrench) disable this and use the pickup key instead.
    /// </summary>
    [RequireComponent(typeof(WorldItem))]
    public class DroppedItemMagnet : MonoBehaviour
    {
        [SerializeField] private PlayerAnchor _playerAnchor;

        [Header("Pickup")]
        [Tooltip("How close the player must be to collect the drop.")]
        [SerializeField] private float _pickupRadius = 2f;

        [Tooltip("Delay before the drop can be collected (lets it settle first).")]
        [SerializeField] private float _collectDelay = 0.1f;

        private WorldItem _item;
        private float _age;
        private float _delay;

        /// <summary>Extra delay before collection (used when player drops an item).</summary>
        public void Suppress(float seconds) => _delay = Mathf.Max(_delay, seconds);

        private void Awake() => _item = GetComponent<WorldItem>();

        private void OnEnable()
        {
            _age = 0f;
            _delay = 0f;
        }

        private void Update()
        {
            ItemDefinition def = _item != null ? _item.Item : null;
            if (def == null) return;

            if (!def.AutoCollects) { enabled = false; return; }

            _age += Time.deltaTime;
            if (_age < _collectDelay + _delay) return;

            Transform player = _playerAnchor != null ? _playerAnchor.Current : null;
            if (player == null) return;

            if (Vector3.Distance(transform.position, player.position) <= _pickupRadius)
                Collect(def);
        }

        private void Collect(ItemDefinition def)
        {
            if (def.isCurrency)
            {
                if (def.currencyTarget != null) def.currencyTarget.Add(_item.Count);
                if (GameEventBus.Instance != null)
                    GameEventBus.Instance.Publish(new CurrencyCollected(def, _item.Count));
                Destroy(gameObject);
                return;
            }

            PlayerInventory.Instance?.TryAddItem(def, _item.Count);
            Destroy(gameObject);
        }
    }

}
