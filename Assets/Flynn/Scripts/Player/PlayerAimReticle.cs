using UnityEngine;

/// <summary>
/// Aim indicator that shows the player's throw/swing direction.
/// The reticle billboards to match the camera-facing sprite, then rotates within
/// that plane using the screen-space angle to the mouse — so it visually orbits
/// the sprite's silhouette edge rather than sweeping on the world XZ ground plane.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerMouseAimer))]
public class PlayerAimReticle : MonoBehaviour
{
    [SerializeField] private WrenchConfig _config;
    [Tooltip("Root pivot positioned at the player and yaw-rotated to the aim direction.")]
    [SerializeField] private Transform _reticleRoot;
    [Tooltip("Flat arrow whose length (local Y) scales with charge.")]
    [SerializeField] private Transform _arrow;
    [SerializeField] private SpriteRenderer _arrowSprite;

    [SerializeField] private Color _idleColor     = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color _chargingColor = new Color(0.3f, 0.8f, 1f, 0.9f);
    [SerializeField] private Color _readyColor    = new Color(1f, 0.55f, 0.1f, 1f);
    [Tooltip("Arrow length multiplier at full charge.")]
    [SerializeField] private float _maxLengthMul = 2.2f;

    private PlayerInventory _inventory;
    private PlayerMouseAimer _aimer;
    private IPlayerVisual _animDriver;
    private Camera _cam;
    private Vector3 _baseArrowScale = Vector3.one;

    private void Awake()
    {
        _inventory  = GetComponent<PlayerInventory>();
        _aimer      = GetComponent<PlayerMouseAimer>();
        _animDriver = GetComponent<IPlayerVisual>();
        _cam        = Camera.main;
        if (_arrow != null) _baseArrowScale = _arrow.localScale;
    }

    private void LateUpdate()
    {
        // Aim reticle disabled — hide and bail.
        if (_reticleRoot != null) _reticleRoot.gameObject.SetActive(false);
    }

    // Wired late (controllers added after this component) — resolve lazily.
    private WrenchSwingController _swing;
    private WrenchThrowController _throw;
    private float SwingCharge()
    {
        if (_swing == null) _swing = GetComponent<WrenchSwingController>();
        return _swing != null && _swing.IsCharging ? _swing.ChargeNormalized : 0f;
    }
    private float ThrowCharge()
    {
        if (_throw == null) _throw = GetComponent<WrenchThrowController>();
        return _throw != null && _throw.IsCharging ? _throw.ChargeNormalized : 0f;
    }
}
