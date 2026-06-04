using UnityEngine;

/// <summary>
/// Left-click swing for the wrench multitool. Tap = light swing; hold to charge and
/// release for a heavier swing (longer cooldown). The swing faces the mouse aim point.
/// When aimed at a ResourceNode the wrench "transforms" to that tool's attack animation,
/// otherwise it plays the plain wrench swing. Disabled while the wrench is airborne
/// (thrown). Exposes IsCharging / ChargeNormalized for the aim reticle.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerMouseAimer))]
public class WrenchSwingController : MonoBehaviour
{
    private const int WrenchAnimIndex = 4;

    [SerializeField] private WrenchConfig _config;
    [Tooltip("Optional — used to block swinging while the wrench is mid-throw.")]
    [SerializeField] private WrenchThrowController _throw;

    private IPlayerVisual _animDriver;

    [Tooltip("Delay from swing release to the hit landing (seconds). Tune for impact feel.")]
    [SerializeField] private float _hitDelay = 0.1f;

    private PlayerInventory _inventory;
    private PlayerMouseAimer _aimer;
    private float _cooldown;
    private float _chargeTimer;
    private bool _charging;

    // Damage lands a short, tunable delay after release (impact timing), not instantly.
    private ResourceNode _pendingNode;
    private int _pendingDamage;
    private float _hitTimer;
    private bool _hitPending;

    public bool IsCharging => _charging;
    public float ChargeNormalized => _config != null && _config.swingChargeTime > 0f
        ? Mathf.Clamp01(_chargeTimer / _config.swingChargeTime)
        : 0f;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _aimer     = GetComponent<PlayerMouseAimer>();
        _animDriver = GetComponent<IPlayerVisual>();
    }

    private void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;

        // Apply a queued hit once its delay elapses.
        if (_hitPending)
        {
            _hitTimer -= Time.deltaTime;
            if (_hitTimer <= 0f) ApplyPendingHit();
        }

        bool usable = _inventory.ActiveItemType == ItemType.Wrench && (_throw == null || !_throw.IsAirborne);
        if (!usable)
        {
            // Wrench put away (or thrown) mid-charge: abort the frozen windup pose.
            if (_charging)
            {
                if (_animDriver != null) _animDriver.CancelSwing();
                _charging = false;
                _chargeTimer = 0f;
            }
            return;
        }

        if (Input.GetMouseButtonDown(0) && _cooldown <= 0f)
        {
            _charging = true;
            _chargeTimer = 0f;
            FaceAim();
            // Freeze the would-be tool on its windup frame; live-swapped while held.
            if (_animDriver != null) _animDriver.BeginChargePose(_aimer.SwingAnimIndex);
        }
        else if (_charging && Input.GetMouseButton(0))
        {
            _chargeTimer += Time.deltaTime;
            FaceAim();
            if (_animDriver != null) _animDriver.UpdateChargePose(_aimer.SwingAnimIndex, ChargeNormalized);
        }
        else if (_charging && Input.GetMouseButtonUp(0))
        {
            DoSwing();
            _charging = false;
            _chargeTimer = 0f;
        }
    }

    private void FaceAim()
    {
        if (_animDriver != null)
            _animDriver.FaceWorldDirection(_aimer.WorldAimPoint - transform.position);
    }

    private void DoSwing()
    {
        if (_animDriver == null) return;

        FaceAim();

        // Tool comes from the surface under the cursor within melee reach (4 = wrench/air).
        int animIndex = _aimer.SwingAnimIndex;
        if (animIndex <= 0) animIndex = WrenchAnimIndex;

        float charge = ChargeNormalized;
        bool heavy = charge >= (_config != null ? _config.heavySwingThreshold : 0.6f);

        // More charge → faster follow-through (the stored power releasing).
        float lightSpd = _config != null ? _config.lightSwingAnimSpeed : 1.0f;
        float heavySpd = _config != null ? _config.heavySwingAnimSpeed : 2.5f;
        _animDriver.ReleaseSwing(animIndex, Mathf.Lerp(lightSpd, heavySpd, charge));

        _cooldown = heavy
            ? (_config != null ? _config.heavySwingCooldown : 0.8f)
            : (_config != null ? _config.lightSwingCooldown : 0.45f);

        // Harvest damage only lands on ResourceNodes; non-resource surfaces just play the anim.
        // Capture the target + damage now, apply after a short tunable delay (impact feel).
        ResourceNode node = _aimer.MeleeResource;
        if (node != null)
        {
            _pendingNode = node;
            _pendingDamage = heavy
                ? (_config != null ? _config.heavySwingDamage : 2)
                : (_config != null ? _config.lightSwingDamage : 1);
            _hitTimer = _hitDelay;
            _hitPending = true;
        }
    }

    private void ApplyPendingHit()
    {
        _hitPending = false;
        if (_pendingNode != null && _pendingDamage > 0)
            _pendingNode.ReceiveHit(_pendingDamage);
        _pendingNode = null;
        _pendingDamage = 0;
    }
}
