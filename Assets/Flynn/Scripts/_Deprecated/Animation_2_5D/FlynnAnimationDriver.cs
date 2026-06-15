using UnityEngine;

/// <summary>
/// Reads SolarpunkCharacterController state and feeds the Animator on the Visual child.
/// 8-direction facing driven by FacingDir (float): 0=Down, 1=DownLeft, 2=Left,
/// 3=UpLeft, 4=Up, 5=UpRight, 6=Right, 7=DownRight.
/// Right-side facing uses explicit sprites (no flip needed for 8-dir run).
/// Non-run animations map diagonals to nearest cardinal and flip when needed.
/// Camera-relative: "Down" = toward the camera.
/// </summary>
[RequireComponent(typeof(SolarpunkCharacterController))]
public class FlynnAnimationDriver : MonoBehaviour, IPlayerVisual
{
    [SerializeField] private SolarpunkCharacterController _controller;
    [SerializeField] private Camera _camera;
    [Tooltip("The child Visual GameObject that holds the SpriteRenderer + Animator.")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField, Range(0.1f, 5f)] private float _visualScale = 1f;

    [Header("Run speed scaling")]
    [Tooltip("Movement speed that maps to 1× animation speed.")]
    [SerializeField] private float _runSpeedBase = 5f;

    [Header("Facing (sprite flip)")]
    [Tooltip("SpriteRenderer to flip. Auto-found on _visualRoot at Awake if left empty.")]
    [SerializeField] private SpriteRenderer _sprite;
    [Tooltip("Min horizontal speed before facing updates.")]
    [SerializeField, Range(0f, 1f)] private float _faceDeadzone = 0.05f;

    [Header("Visual Center (for throw origin & aim reticle)")]
    [Tooltip("Half the sprite's visible height in world units. Used to compute the billboard's visual centre for camera-corrected spawn/reticle positions.")]
    [SerializeField, Range(0f, 3f)] private float _spriteHalfHeight = 0.5f;

    [Header("Normal Maps")]
    [Tooltip("Material whose _BumpMap will be swapped at runtime.")]
    [SerializeField] private Material _spriteMaterial;
    [Tooltip("Normal map for idle / non-run states.")]
    [SerializeField] private Texture _idleNormal;
    [Tooltip("Normal map for the run state.")]
    [SerializeField] private Texture _runNormal;
    [Tooltip("Normal map for the swing state.")]
    [SerializeField] private Texture _swingNormal;

    // ── Cached references ──────────────────────────────────────────────────────

    private Animator _animator;

    // Held while idle so the blend tree doesn't snap back to Down on release.
    private int _lastFacingDir;
    private bool _lastFlipX;
    private string _lastStateName;

    // Locked while swinging to prevent Update() from overwriting FacingDir.
    private bool _swingFacingLocked;

    // ── Animator parameter hashes ─────────────────────────────────────────────

    private static readonly int SpeedHash       = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash  = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash        = Animator.StringToHash("Jump");
    private static readonly int SwingHash       = Animator.StringToHash("Swing");
    private static readonly int SwingWindupHash = Animator.StringToHash("SwingWindup");
    private static readonly int SwingChargingHash = Animator.StringToHash("SwingCharging");
    private static readonly int HoldHash        = Animator.StringToHash("Hold");
    private static readonly int ThrowHash       = Animator.StringToHash("Throw");
    private static readonly int ScanHash        = Animator.StringToHash("Scan");
    private static readonly int FacingDirHash   = Animator.StringToHash("FacingDir");

    // ── IPlayerVisual ─────────────────────────────────────────────────────────

    public Vector3 VisualCenter =>
        _visualRoot != null
            ? _visualRoot.position + _visualRoot.up * _spriteHalfHeight
            : transform.position;

    public Vector3 VisualGroundCenter =>
        new Vector3(VisualCenter.x, transform.position.y, VisualCenter.z);

    public bool IsAttacking =>
        _animator != null &&
        (IsInSwingState(_animator.GetCurrentAnimatorStateInfo(0)) ||
         (_animator.IsInTransition(0) &&
          IsInSwingState(_animator.GetNextAnimatorStateInfo(0))));

    private bool IsInSwingState(AnimatorStateInfo info) =>
        StateNameStartsWith(info, "Swing") || StateNameStartsWith(info, "SwingWindup") || StateNameStartsWith(info, "SwingRelease");

    public void TriggerAttack(int animIndex)
    {
        // Legacy tool-index attack stubbed — use TriggerSwing instead.
    }

    public void BeginChargePose(int animIndex)
    {
        // Legacy charge windup stubbed.
    }

    public void UpdateChargePose(int animIndex, float charge01)
    {
        // Legacy charge scrub stubbed.
    }

    public void ReleaseSwing(int animIndex, float animSpeed)
    {
        if (_animator == null) return;
        _animator.SetBool(SwingChargingHash, false);
        // SwingCharging IfNot triggers SwingWindup→SwingRelease transition.
        // For quick taps not in windup, fire Swing trigger to reach Swing_* directly.
        if (!StateNameStartsWith(_animator.GetCurrentAnimatorStateInfo(0), "SwingWindup"))
            _animator.SetTrigger(SwingHash);
    }

    public void BeginSwingWindup()
    {
        if (_animator == null) return;
        _swingFacingLocked = true;
        _animator.SetBool(SwingChargingHash, true);
        _animator.SetTrigger(SwingWindupHash);
    }

    public void CancelSwingWindup()
    {
        if (_animator == null) return;
        _swingFacingLocked = false;
        _animator.SetBool(SwingChargingHash, false);
    }

    public void CancelSwing()
    {
        if (_animator == null) return;
        _swingFacingLocked = false;
        _animator.SetBool(SwingChargingHash, false);
        _animator.Play("Idle", 0, 0f);
    }

    /// <summary>
    /// Sets FacingDir from a world-space direction (e.g. player→mouse aim point).
    /// Used by WrenchSwingController to make the swing face where the player clicks.
    /// </summary>
    public void FaceWorldDirection(Vector3 worldDir)
    {
        if (_animator == null || _camera == null) return;

        int dir = WorldDirectionToFacingDir(worldDir);
        _animator.SetInteger(FacingDirHash, dir);
        _lastFacingDir = dir;
        _lastFlipX = false;
        if (_sprite != null) _sprite.flipX = false;
    }

    /// <summary>
    /// Converts a world-space direction vector into an 8-sector FacingDir (0–7)
    /// using the same camera-relative sector logic as the locomotion Update.
    /// </summary>
    private int WorldDirectionToFacingDir(Vector3 worldDir)
    {
        Vector3 camFwd   = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;

        float intoScreen  = Vector3.Dot(worldDir, camFwd);
        float screenRight = Vector3.Dot(worldDir, camRight);

        float angle = Mathf.Atan2(-screenRight, -intoScreen) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        bool flip = angle > 180f;
        if (flip) angle = 360f - angle;

        int dir;
        if      (angle < 22.5f)  dir = 0;
        else if (angle < 67.5f)  dir = 1;
        else if (angle < 112.5f) dir = 2;
        else if (angle < 157.5f) dir = 3;
        else                     dir = 4;

        if (flip)
        {
            dir = dir switch
            {
                1 => 7,
                2 => 6,
                3 => 5,
                _ => dir
            };
        }

        return dir;
    }

    // ── Frame-by-frame trigger API ────────────────────────────────────────────

    public void TriggerJump()
    {
        Debug.Log($"[Anim] Jump (animator={(_animator != null ? "ok" : "NULL")})");
        if (_animator == null) return;
        _animator.SetTrigger(JumpHash);
    }

    public void TriggerSwing()
    {
        if (_animator == null) return;
        _animator.SetBool(SwingChargingHash, false);
        _animator.SetTrigger(SwingHash);
    }

    public void SetHolding(bool holding)
    {
        Debug.Log($"[Anim] Hold = {holding} (animator={(_animator != null ? "ok" : "NULL")})");
        if (_animator == null) return;
        _animator.SetBool(HoldHash, holding);
    }

    public void TriggerThrow()
    {
        Debug.Log($"[Anim] Throw (animator={(_animator != null ? "ok" : "NULL")})");
        if (_animator == null) return;
        _animator.SetTrigger(ThrowHash);
    }

    public void TriggerScan()
    {
        Debug.Log($"[Anim] Scan (animator={(_animator != null ? "ok" : "NULL")})");
        if (_animator == null) return;
        _animator.SetTrigger(ScanHash);
    }

    // ── Unity messages ────────────────────────────────────────────────────────

    private void Awake()
    {
        // Get Animator from the Visual child (not self)
        if (_visualRoot != null)
        {
            _animator = _visualRoot.GetComponent<Animator>();
            if (_sprite == null) _sprite = _visualRoot.GetComponentInChildren<SpriteRenderer>();
            _visualRoot.localScale = Vector3.one * _visualScale;
        }
        if (_camera == null) _camera = Camera.main;
        Debug.Log($"[Anim] Awake — visualRoot={(_visualRoot != null ? _visualRoot.name : "NULL")}, animator={(_animator != null ? "ok" : "NULL")}, sprite={(_sprite != null ? _sprite.name : "NULL")}");
    }

    private void Update()
    {
        if (_animator == null || _controller == null) return;

        Vector3 velocity = _controller.MoveInput;
        Vector2 xz = new Vector2(velocity.x, velocity.z);

        // Feed Speed and IsGrounded for locomotion
        _animator.SetFloat(SpeedHash, xz.magnitude);
        _animator.SetBool(IsGroundedHash, _controller.IsGrounded);

        // ── Camera-relative facing direction ──────────────────────────────────
        // Down = toward camera, Up = away from camera, Left = screen-left
        if (!_swingFacingLocked)
        {
            if (_camera != null && xz.sqrMagnitude >= _faceDeadzone * _faceDeadzone)
            {
                int dir = WorldDirectionToFacingDir(velocity);

                _animator.SetInteger(FacingDirHash, dir);
                if (_sprite != null) _sprite.flipX = false;

                _lastFacingDir = dir;
                _lastFlipX = false;
            }
            else
            {
                // Hold last facing when idle
                _animator.SetInteger(FacingDirHash, _lastFacingDir);
                if (_sprite != null) _sprite.flipX = _lastFlipX;
            }
        }

        // Unlock facing once the swing chain finishes
        if (_swingFacingLocked && !IsAttacking)
            _swingFacingLocked = false;

        // ── Scale Run animation speed by actual horizontal velocity ────────────
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (StateNameStartsWith(stateInfo, "Run"))
        {
            var rb = _controller.GetComponent<Rigidbody>();
            float hSpeed = rb != null
                ? new Vector2(rb.velocity.x, rb.velocity.z).magnitude
                : xz.magnitude;
            _animator.speed = Mathf.Max(hSpeed / _runSpeedBase, 0.25f);
        }
        else if (StateNameStartsWith(stateInfo, "SwingWindup"))
        {
            // Play the windup slowly through its frames without looping
            _animator.speed = 0.3f;
        }
        else
        {
            _animator.speed = 1f;
        }

        // ── Swap normal map based on current state ─────────────────────────────
        SwapNormalMap(stateInfo);
    }

    // ── Normal map swapping ───────────────────────────────────────────────────

    private void SwapNormalMap(AnimatorStateInfo stateInfo)
    {
        if (_spriteMaterial == null) return;

        string stateName = null;
        if (StateNameStartsWith(stateInfo, "Run")) stateName = "Run";
        else if (StateNameStartsWith(stateInfo, "Swing") || StateNameStartsWith(stateInfo, "SwingWindup") || StateNameStartsWith(stateInfo, "SwingRelease")) stateName = "Swing";
        else if (StateNameStartsWith(stateInfo, "Idle")) stateName = "Idle";

        if (stateName == _lastStateName) return;
        _lastStateName = stateName;

        Texture normal;
        if (stateName == "Run") normal = _runNormal;
        else if (stateName == "Swing") normal = _swingNormal;
        else normal = _idleNormal;

        if (normal != null)
            _spriteMaterial.SetTexture("_NormalMap", normal);
    }

    /// <summary>
    /// Checks if the current animator state name starts with the given prefix.
    /// Needed because 8-dir states are named "Idle_Down", "Run_UpLeft", etc.
    /// </summary>
    private bool StateNameStartsWith(AnimatorStateInfo stateInfo, string prefix)
    {
        string[] dirSuffixes = { "_Down", "_DownLeft", "_Left", "_UpLeft", "_Up", "_UpRight", "_Right", "_DownRight" };
        foreach (var suffix in dirSuffixes)
        {
            if (stateInfo.IsName(prefix + suffix))
                return true;
        }
        // Also check bare name (e.g. "Idle", "Run") for legacy/blend-tree compatibility
        if (stateInfo.IsName(prefix))
            return true;
        return false;
    }
}
