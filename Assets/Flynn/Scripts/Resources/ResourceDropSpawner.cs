using UnityEngine;

/// <summary>
/// Ejects a <see cref="ResourceNode"/>'s drops when it's depleted, rolling each <see cref="DropEntry"/>
/// in the node's config and spawning the results through <see cref="WorldItemSpawner"/> with a small
/// upward pop so they scatter and settle before flying to the player. Replaces the old inline
/// SpawnDrops on ResourceNode (single responsibility).
/// </summary>
[RequireComponent(typeof(ResourceNode))]
public class ResourceDropSpawner : MonoBehaviour
{
    [Tooltip("Upward impulse applied to each spawned drop so it pops out of the node.")]
    [SerializeField] private float _popImpulse = 1.1f;
    [Tooltip("Height above the node base that drops spawn from.")]
    [SerializeField] private float _spawnHeight = 0.25f;

    private ResourceNode _node;

    private void Awake() => _node = GetComponent<ResourceNode>();

    private void OnEnable()  { _node.OnDepleted += SpawnDrops; }
    private void OnDisable() { if (_node != null) _node.OnDepleted -= SpawnDrops; }

    private void SpawnDrops()
    {
        _node.OnDepleted -= SpawnDrops;

        ResourceNodeConfig cfg = _node.Config;
        if (cfg == null || cfg.drops == null) return;

        foreach (DropEntry drop in cfg.drops)
        {
            if (drop.item == null) continue;
            if (Random.value > drop.dropChance) continue;

            int count = Random.Range(drop.minCount, Mathf.Max(drop.minCount, drop.maxCount) + 1);
            for (int i = 0; i < count; i++)
            {
                Vector2 scatter = Random.insideUnitCircle * cfg.dropScatterRadius;
                Vector3 pos = transform.position + new Vector3(scatter.x, _spawnHeight, scatter.y);
                WorldItemSpawner.Spawn(drop.item, 1, pos, _popImpulse);
            }
        }
    }
}
