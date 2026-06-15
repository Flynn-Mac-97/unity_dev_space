using UnityEngine;

/// <summary>
/// Add to any GameObject that should cast a 2D silhouette shadow.
/// Auto-registers with Shadow2DManager on enable and unregisters on disable.
/// Works in Edit Mode so shadows are visible in the Scene view.
/// </summary>
[ExecuteInEditMode]
public class Shadow2DTarget : MonoBehaviour
{
    [Tooltip("Override transform used as the shadow origin. Leave null to use this transform.")]
    [SerializeField] private Transform _overrideTransform;

    [Tooltip("Multiplier on shadow size.")]
    [SerializeField] private float _scaleMultiplier = 1f;

    [Tooltip("Vertical lift — shifts the shadow downward by this amount, " +
             "making the object appear to float or overhang above the ground.")]
    [SerializeField, Range(0f, 5f)] private float _liftHeight;

    private Renderer _renderer;
    private SpriteRenderer _spriteRenderer;
    private Shadow2DFloating _floating;

    public Transform TargetTransform => _overrideTransform != null ? _overrideTransform : transform;
    public float ShadowScale => _scaleMultiplier;

    /// <summary>Runtime height cue set by gameplay (e.g. PlatformerController2D feeds
    /// the player's current height above the surface below). Added to the serialized
    /// lift so a jumping object's shadow stays pinned on the ground. The widening
    /// feet→shadow gap is exactly the jump/fall height.</summary>
    public float DynamicLift { get; set; }

    public float ShadowLift => _liftHeight + DynamicLift;
    public Shadow2DFloating Floating => _floating ??= GetComponent<Shadow2DFloating>();

    private SpriteRenderer SourceRenderer
    {
        get
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            return _spriteRenderer;
        }
    }

    public Sprite CurrentSprite => SourceRenderer != null ? SourceRenderer.sprite : null;
    public bool FlipX => SourceRenderer != null && SourceRenderer.flipX;

    public Bounds RendererBounds
    {
        get
        {
            if (_renderer == null)
                _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
                return _renderer.bounds;
            return new Bounds(transform.position, Vector3.one);
        }
    }

    private void OnEnable()
    {
        var manager = FindObjectOfType<Shadow2DManager>();
        if (manager != null)
            manager.Register(this);
    }

    private void OnDisable()
    {
        var manager = FindObjectOfType<Shadow2DManager>();
        if (manager != null)
            manager.Unregister(this);
    }
}
