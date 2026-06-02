using UnityEngine;

/// <summary>
/// Marks a world object as a harvestable resource and manages its runtime health.
///
/// All static data (required tool, max health, drops) lives in the assigned
/// <see cref="ResourceNodeConfig"/> ScriptableObject. Assign one asset per
/// resource type (Tree_Config, Stone_Config, etc.) in the Inspector.
///
/// Hover detection is driven by <see cref="PlayerMouseAimer"/>; damage is
/// applied by calling <see cref="ReceiveHit"/>.
///
/// AttackAnimIndex maps RequiredTool → Animator AttackIndex (1-4):
///   Pick = 1 | Axe = 2 | Hammer = 3 | Wrench = 4
/// </summary>
public class ResourceNode : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("Data asset for this resource type (health, tool, drops). Create via Flynn/Resource/Node Config.")]
    [SerializeField] private ResourceNodeConfig _config;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised after each successful hit. Argument is remaining health.</summary>
    public event System.Action<int> OnDamaged;

    /// <summary>Raised when health reaches zero, before drops are spawned.</summary>
    public event System.Action OnDepleted;

    // ── Public properties ─────────────────────────────────────────────────────

    public ResourceNodeConfig Config => _config;

    /// <summary>Current remaining health this session.</summary>
    public int CurrentHealth { get; private set; }

    /// <summary>Convenience accessor so callers don't have to null-check Config.</summary>
    public ItemType RequiredTool => _config != null ? _config.requiredTool : ItemType.Pick;

    /// <summary>Display label for hover UI.</summary>
    public string DisplayName => _config != null ? _config.displayName : "Resource";

    /// <summary>
    /// Animator AttackIndex value for this resource's required tool.
    /// Returns 0 when no valid tool is mapped.
    /// </summary>
    public int AttackAnimIndex => RequiredTool switch
    {
        ItemType.Pick   => 1,
        ItemType.Axe    => 2,
        ItemType.Hammer => 3,
        ItemType.Wrench => 4,
        _               => 0,
    };

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        CurrentHealth = _config != null ? _config.maxHealth : 1;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply <paramref name="damage"/> points to this node. When health reaches
    /// zero the node spawns its drops and destroys itself.
    /// </summary>
    /// <param name="damage">Positive integer damage value.</param>
    public void ReceiveHit(int damage)
    {
        if (CurrentHealth <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        OnDamaged?.Invoke(CurrentHealth);

        if (CurrentHealth <= 0)
            Deplete();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Deplete()
    {
        OnDepleted?.Invoke();
        SpawnDrops();
        Destroy(gameObject);
    }

    private void SpawnDrops()
    {
        if (_config == null) return;

        foreach (DropEntry drop in _config.drops)
        {
            if (drop.prefab == null) continue;
            if (Random.value > drop.dropChance) continue;

            int count = Random.Range(drop.minCount, Mathf.Max(drop.minCount, drop.maxCount) + 1);
            for (int i = 0; i < count; i++)
            {
                Vector2 scatter   = Random.insideUnitCircle * _config.dropScatterRadius;
                Vector3 spawnPos  = transform.position + new Vector3(scatter.x, 0f, scatter.y);
                Instantiate(drop.prefab, spawnPos, Quaternion.identity);
            }
        }
    }
}
