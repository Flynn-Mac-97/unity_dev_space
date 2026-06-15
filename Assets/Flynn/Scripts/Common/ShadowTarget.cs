using UnityEngine;

/// <summary>
/// Add to any GameObject that should cast a fake drop-shadow.
/// Auto-registers with ShadowManager on enable and unregisters on disable.
/// Works in Edit Mode so shadows are visible in the Scene view.
/// </summary>
[ExecuteInEditMode]
public class ShadowTarget : MonoBehaviour, IShadowTarget
{
    [Tooltip("Override transform used as the shadow origin. Leave null to use this transform.")]
    [SerializeField] private Transform _overrideTransform;

    [Tooltip("Multiplier applied on top of the auto-detected sprite bounds size.")]
    [SerializeField] private float _scaleMultiplier = 1f;

    private Renderer _renderer;
    private SpriteRenderer _spriteRenderer;

    public Transform TargetTransform => _overrideTransform != null ? _overrideTransform : transform;
    public float ShadowScale => _scaleMultiplier;

    private SpriteRenderer SourceRenderer
    {
        get
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            return _spriteRenderer;
        }
    }

    /// <summary>The sprite currently shown — drives the shadow silhouette so it tracks animation.</summary>
    public Sprite CurrentSprite => SourceRenderer != null ? SourceRenderer.sprite : null;

    /// <summary>Mirror of the source SpriteRenderer's flip so the shadow matches facing.</summary>
    public bool FlipX => SourceRenderer != null && SourceRenderer.flipX;

    /// <summary>
    /// Returns the world-space bounds of the first Renderer on this object.
    /// Falls back to a 1×1×1 bounds centred on the transform if none found.
    /// </summary>
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
        var manager = FindObjectOfType<ShadowManager>();
        if (manager != null)
            manager.Register(this);
    }

    private void OnDisable()
    {
        var manager = FindObjectOfType<ShadowManager>();
        if (manager != null)
            manager.Unregister(this);
    }
}
