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
    [SerializeField] private FlynnAnimationDriver _animDriver;
    [Tooltip("Optional — used to block swinging while the wrench is mid-throw.")]
    [SerializeField] private WrenchThrowController _throw;

    private PlayerInventory _inventory;
    private PlayerMouseAimer _aimer;
    private float _cooldown;
    private float _chargeTimer;
    private bool _charging;

    public bool IsCharging => _charging;
    public float ChargeNormalized => _config != null && _config.swingChargeTime > 0f
        ? Mathf.Clamp01(_chargeTimer / _config.swingChargeTime)
        : 0f;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _aimer     = GetComponent<PlayerMouseAimer>();
    }

    private void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;

        bool usable = _inventory.ActiveItemType == ItemType.Wrench && (_throw == null || !_throw.IsAirborne);
        if (!usable)
        {
            _charging = false;
            _chargeTimer = 0f;
            return;
        }

        if (Input.GetMouseButtonDown(0) && _cooldown <= 0f)
        {
            _charging = true;
            _chargeTimer = 0f;
        }
        else if (_charging && Input.GetMouseButton(0))
        {
            _chargeTimer += Time.deltaTime;
        }
        else if (_charging && Input.GetMouseButtonUp(0))
        {
            DoSwing();
            _charging = false;
            _chargeTimer = 0f;
        }
    }

    private void DoSwing()
    {
        if (_animDriver == null) return;

        _animDriver.FaceWorldDirection(_aimer.WorldAimPoint - transform.position);

        int animIndex = _aimer.HoveredResource != null
            ? _aimer.HoveredResource.AttackAnimIndex
            : WrenchAnimIndex;
        if (animIndex <= 0) return;

        bool heavy = ChargeNormalized >= (_config != null ? _config.heavySwingThreshold : 0.6f);
        _animDriver.TriggerAttack(animIndex);
        _cooldown = heavy
            ? (_config != null ? _config.heavySwingCooldown : 0.8f)
            : (_config != null ? _config.lightSwingCooldown : 0.45f);

        // Apply harvest damage to the hovered resource node.
        if (_aimer.HoveredResource != null)
        {
            int damage = heavy
                ? (_config != null ? _config.heavySwingDamage : 2)
                : (_config != null ? _config.lightSwingDamage : 1);
            _aimer.HoveredResource.ReceiveHit(damage);
        }
    }
}
