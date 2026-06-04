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

    [Tooltip("Channel raised on each hit so the HUD can show a world-anchored HP bar. Optional.")]
    [SerializeField] private ResourceHitChannel _hitChannel;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised after each successful hit. Argument is remaining health.</summary>
    public event System.Action<int> OnDamaged;

    /// <summary>Raised when health reaches zero, before drops are spawned.</summary>
    public event System.Action OnDepleted;

    // ── Public properties ─────────────────────────────────────────────────────

    public ResourceNodeConfig Config => _config;

    /// <summary>Current remaining health this session.</summary>
    public int CurrentHealth { get; private set; }

    /// <summary>Maximum health from the config (1 when unconfigured).</summary>
    public int MaxHealth => _config != null ? Mathf.Max(1, _config.maxHealth) : 1;

    /// <summary>Remaining health as a 0-1 fraction.</summary>
    public float Health01 => Mathf.Clamp01((float)CurrentHealth / MaxHealth);

    private bool _depleted;

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
        if (_hitChannel != null) _hitChannel.Raise(this, Health01);

        if (CurrentHealth <= 0)
            Deplete();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Node is exhausted. Raises <see cref="OnDepleted"/> for the drop-spawner and death-fall
    /// listeners to react (eject drops, topple + fade). The node only destroys itself here as a
    /// fallback when no <see cref="ResourceDeathFall"/> is present to own the removal.
    /// </summary>
    private void Deplete()
    {
        if (_depleted) return;
        _depleted = true;

        OnDepleted?.Invoke();

        if (GetComponent<ResourceDeathFall>() == null)
            Destroy(gameObject);
    }
}
