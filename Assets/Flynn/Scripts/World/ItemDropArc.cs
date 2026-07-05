using System.Collections;
using UnityEngine;
using Flynn.Common;
using Flynn.Core;
using Flynn.Events;
using Flynn.Player;

namespace Flynn.World
{
    /// <summary>
    /// Animates a dropped item along a parabolic arc from the resource toward the player.
    /// Tracks the player's live position during flight and collects directly on arrival —
    /// the item never exists on the ground.
    /// </summary>
    [RequireComponent(typeof(WorldItem))]
    public class ItemDropArc : MonoBehaviour
    {
        [SerializeField] private PlayerAnchor _playerAnchor;
        [SerializeField] private ItemDropArcConfig _config;

        private WorldItem _item;
        private SpriteRenderer[] _renderers;
        private MonoBehaviour _sortableSprite;
        private MonoBehaviour _magnet;

        private void Awake()
        {
            _item = GetComponent<WorldItem>();
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);

            foreach (var comp in GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                string name = comp.GetType().Name;
                if (name == "SortableSprite") _sortableSprite = comp;
                else if (name == "DroppedItemMagnet") _magnet = comp;
            }
        }

        private Vector3 _baseScale;

        public void BeginArc(Vector3 origin)
        {
            if (_config == null)
            {
                Debug.LogError("[ItemDropArc] No config assigned on " + gameObject.name);
                Collect();
                return;
            }

            transform.localScale *= _config.scaleMultiplier;
            _baseScale = transform.localScale;

            if (_sortableSprite != null) _sortableSprite.enabled = false;
            if (_magnet != null) _magnet.enabled = false;

            foreach (var sr in _renderers)
            {
                if (sr != null)
                {
                    sr.sortingLayerName = "Default";
                    sr.sortingOrder = 10000;
                }
            }

            StartCoroutine(ArcRoutine(origin));
        }

        private IEnumerator ArcRoutine(Vector3 origin)
        {
            float elapsed = 0f;

            while (elapsed < _config.duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _config.duration);

                float easedT = _config.horizontalCurve.Evaluate(t);
                float heightT = _config.verticalCurve.Evaluate(t);

                Transform player = _playerAnchor != null ? _playerAnchor.Current : null;
                Vector3 target = player != null ? player.position : origin;

                Vector3 pos = Vector3.Lerp(origin, target, easedT);
                pos.y += _config.arcHeight * heightT;

                transform.position = pos;

                // Pre-absorb pop: swell just before reaching the player, then
                // shrink into them — sells the "collected" beat without a UI hit.
                if (t > 0.85f)
                    transform.localScale = _baseScale * Mathf.Lerp(1.35f, 0.3f, (t - 0.85f) / 0.15f);
                else if (t > 0.7f)
                    transform.localScale = _baseScale * Mathf.Lerp(1f, 1.35f, (t - 0.7f) / 0.15f);

                yield return null;
            }

            Collect();
        }

        private void Collect()
        {
            ItemDefinition def = _item != null ? _item.Item : null;
            if (def == null)
            {
                Destroy(gameObject);
                return;
            }

            if (def.isCurrency)
            {
                if (def.currencyTarget != null) def.currencyTarget.Add(_item.Count);
                if (GameEventBus.Instance != null)
                    GameEventBus.Instance.Publish(new CurrencyCollected(def, _item.Count));
            }
            else
            {
                PlayerInventory.Instance?.TryAddItem(def, _item.Count);
            }

            Destroy(gameObject);
        }
    }
}
