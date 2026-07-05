using UnityEngine;


using Flynn.Core;
using Flynn.Player;
using Flynn.UI.Core;

using Flynn.Player.Interaction;
namespace Flynn.World
{
    /// <summary>
    /// Single entry point for putting an item into the world. Resource drops, echo-shard rolls, the
    /// drop key and drag-to-drop all spawn through here so there's one place that wires up the
    /// WorldItem + the initial "pop" physics. The prefab is resolved from the item itself
    /// (<see cref="ItemDefinition.worldPrefab"/>), so callers only need the item + count.
    /// </summary>
    public static class WorldItemSpawner
    {
        /// <summary>
        /// Spawn a stack of <paramref name="item"/> at <paramref name="position"/>. Optionally apply an
        /// upward "pop" impulse (requires a Rigidbody on the prefab) so drops scatter and settle.
        /// Returns the spawned WorldItem, or null if the item has no world prefab.
        /// </summary>
        public static WorldItem Spawn(ItemDefinition item, int count, Vector3 position, float popImpulse = 0f)
        {
            if (item == null || item.worldPrefab == null || count <= 0) return null;

            GameObject go = Object.Instantiate(item.worldPrefab, position, Quaternion.identity);

            WorldItem worldItem = go.GetComponent<WorldItem>();
            if (worldItem != null) worldItem.Configure(item, count);

            if (popImpulse > 0f)
            {
                Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                {
                    Vector2 dir = (Vector2.up + 0.45f * Random.insideUnitCircle).normalized;
                    rb.AddForce(dir * popImpulse, ForceMode2D.Impulse);
                }
            }

            return worldItem;
        }

        /// <summary>
        /// Spawn an item that arcs from <paramref name="origin"/> toward the player via a
        /// parabolic flight path. Requires an <see cref="ItemDropArc"/> component on the prefab.
        /// </summary>
        public static WorldItem SpawnArcToPlayer(ItemDefinition item, int count, Vector3 origin)
        {
            if (item == null || item.worldPrefab == null || count <= 0) return null;

            GameObject go = Object.Instantiate(item.worldPrefab, origin, Quaternion.identity);

            WorldItem worldItem = go.GetComponent<WorldItem>();
            if (worldItem != null) worldItem.Configure(item, count);

            ItemDropArc arc = go.GetComponent<ItemDropArc>();
            if (arc != null)
                arc.BeginArc(origin);
            else
                UnityEngine.Debug.LogWarning($"[WorldItemSpawner] Prefab for '{item.itemId}' has no ItemDropArc — falling back to static spawn at {origin}.");

            return worldItem;
        }
    }

}
