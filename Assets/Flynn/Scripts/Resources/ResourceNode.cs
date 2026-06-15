using UnityEngine;
using UnityEngine.Events;
using Flynn.Events;

/// <summary>
/// Marks a world object as a harvestable resource and manages its runtime health.
/// Subscribes to <see cref="ResourceManager.OnResourceHit"/> so it only takes damage
/// when the player's hit targets this object (or a child collider). All static data
/// (required tool, max health, drops) lives in the assigned
/// <see cref="ResourceNodeConfig"/> ScriptableObject.
///
/// Designer-hookable events are exposed as UnityEvents in the Inspector:
///   • <see cref="OnHitEvent"/>      — after each hit that damages this node
///   • <see cref="OnDepletedEvent"/>  — when health reaches zero
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

    // ── UnityEvents (designer-hookable) ───────────────────────────────────────

    [Header("Events")]
    [Tooltip("Fired after each hit that damages this node. Argument = remaining health.")]
    [SerializeField] private IntEvent _onHitEvent = new IntEvent();

    [Tooltip("Fired when health reaches zero (before drops / destruction).")]
    [SerializeField] private UnityEvent _onDepletedEvent = new UnityEvent();

    // ── C# events (code-side subscribers) ─────────────────────────────────────

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

    /// <summary>Resource type for effectiveness lookups.</summary>
    public ResourceType ResourceType => _config != null ? _config.resourceType : ResourceType.None;

    /// <summary>Whether this node has been fully depleted this session.</summary>
    public bool IsDepleted => _depleted;

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

    private void OnEnable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceHit += HandleResourceManagerHit;
    }

    private void OnDisable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceHit -= HandleResourceManagerHit;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Try to apply a tool hit to this node. Looks up tool effectiveness from the
    /// <see cref="ToolEffectivenessTable"/> on the <see cref="ResourceManager"/>.
    /// If the tool is ineffective (multiplier 0), the hit is rejected and a
    /// <see cref="ResourceHit"/> event is published with zero damage for "wrong tool" feedback.
    /// Returns true if the hit was accepted (even if no damage was dealt).
    /// </summary>
    public bool TryApplyToolHit(ToolHitContext hit)
    {
        if (_depleted) return false;

        float effectiveness = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetEffectiveness(hit.ToolType, ResourceType)
            : 1f;

        int scaledDamage = Mathf.RoundToInt(hit.Damage * effectiveness);

        // Publish hit event regardless (for "wrong tool" feedback)
        PublishEvent(new ResourceHit(this, hit));

        if (scaledDamage <= 0) return false;

        ReceiveHit(scaledDamage);
        return true;
    }

    /// <summary>
    /// Apply <paramref name="damage"/> points to this node. When health reaches
    /// zero the node spawns its drops and destroys itself.
    /// </summary>
    /// <param name="damage">Positive integer damage value.</param>
    public void ReceiveHit(int damage)
    {
        if (CurrentHealth <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);

        // C# event (code subscribers)
        OnDamaged?.Invoke(CurrentHealth);

        // UnityEvent (designer-hooked effects)
        _onHitEvent?.Invoke(CurrentHealth);

        // HUD channel
        if (_hitChannel != null) _hitChannel.Raise(this, Health01);

        // GameEventBus (decoupled feedback/audio/UI)
        PublishEvent(new ResourceDamaged(this, damage, CurrentHealth));

        if (CurrentHealth <= 0)
            Deplete();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Listener for <see cref="ResourceManager.OnResourceHit"/>. Only applies
    /// damage when the hit targets this object (or a child collider).
    /// </summary>
    private void HandleResourceManagerHit(PlayerHitInfo info)
    {
        if (info.Target == null) return;
        if (!IsThisResource(info.Target)) return;
        ReceiveHit(info.Power);
    }

    /// <summary>True if the target GameObject is this object or a descendant.</summary>
    private bool IsThisResource(GameObject target)
    {
        Transform t = target.transform;
        while (t != null)
        {
            if (t.gameObject == gameObject) return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>
    /// Node is exhausted. Raises events for the drop-spawner and death-fall
    /// listeners to react (eject drops, topple + fade). The node only destroys
    /// itself here as a fallback when no <see cref="ResourceDeathFall"/> is present
    /// to own the removal.
    /// </summary>
    private void Deplete()
    {
        if (_depleted) return;
        _depleted = true;

        // C# event (code subscribers)
        OnDepleted?.Invoke();

        // UnityEvent (designer-hooked effects)
        _onDepletedEvent?.Invoke();

        // GameEventBus (decoupled feedback/audio/UI)
        PublishEvent(new ResourceDepleted(this));

        if (GetComponent<ResourceDeathFall>() == null)
            Destroy(gameObject);
    }

    // ── Nested types ──────────────────────────────────────────────────────────

    /// <summary>UnityEvent passing remaining health as an int.</summary>
    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }

    private static void PublishEvent<T>(T evt) where T : struct
    {
        if (GameEventBus.Instance != null) GameEventBus.Instance.Publish(evt);
    }
}
