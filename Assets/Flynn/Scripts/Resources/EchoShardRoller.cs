using UnityEngine;

/// <summary>
/// Rolls a chance on every hit to drop an echo shard (a currency item) from a <see cref="ResourceNode"/>.
/// The shard spawns as a <see cref="WorldItem"/> that magnets to the player and adds to the echo-shard
/// counter rather than the inventory. Chance and shard item come from the node's config.
/// </summary>
[RequireComponent(typeof(ResourceNode))]
public class EchoShardRoller : MonoBehaviour
{
    [Tooltip("Upward impulse applied to the spawned shard.")]
    [SerializeField] private float _popImpulse = 2f;
    [SerializeField] private float _spawnHeight = 0.8f;

    private ResourceNode _node;

    private void Awake() => _node = GetComponent<ResourceNode>();

    private void OnEnable()  { _node.OnDamaged += HandleDamaged; }
    private void OnDisable() { if (_node != null) _node.OnDamaged -= HandleDamaged; }

    private void HandleDamaged(int remaining)
    {
        ResourceNodeConfig cfg = _node.Config;
        if (cfg == null || cfg.echoShardItem == null) return;
        if (Random.value > cfg.echoShardChancePerHit) return;

        Vector2 jitter = Random.insideUnitCircle * 0.2f;
        Vector3 pos = transform.position + new Vector3(jitter.x, _spawnHeight, jitter.y);
        WorldItemSpawner.Spawn(cfg.echoShardItem, 1, pos, _popImpulse);
    }
}
