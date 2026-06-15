using System.Collections.Generic;
using UnityEngine;

public interface IShadowTarget
{
    Transform TargetTransform { get; }
    float ShadowScale { get; }
    Bounds RendererBounds { get; }
    Sprite CurrentSprite { get; }
    bool FlipX { get; }
}

/// <summary>
/// Casts flat ground-projected silhouette shadows for 2.5D billboarded sprites.
///
/// Each registered IShadowTarget gets a pooled shadow sprite that is laid flat on
/// the XZ ground at the target's feet, given the target's live sprite, oriented
/// along the sun's ground azimuth, and stretched by the sun's elevation (low sun =
/// long shadow). The Flynn/ProjectedShadow shader stamps it as a uniform dark
/// silhouette. Opacity fades as the object rises off the ground.
/// </summary>
[DefaultExecutionOrder(-90)]
[ExecuteInEditMode]
public class ShadowManager : MonoBehaviour
{
    // ── Config ──────────────────────────────────────────────────────────────

    [Header("Ground")]
    [Tooltip("Y position of the ground plane where shadows land.")]
    [SerializeField] private float _groundY = 0f;

    [Tooltip("If true, raycasts downward to find the ground instead of using _groundY.")]
    [SerializeField] private bool _useRaycast;

    [Tooltip("Layer mask for ground raycasts.")]
    [SerializeField] private LayerMask _groundLayer;

    [Tooltip("Max raycast distance.")]
    [SerializeField] private float _raycastMaxDist = 50f;

    [Header("Sun (direction source)")]
    [Tooltip("Directional Light whose angle drives shadow direction + length. " +
             "Leave null to use the fallback euler below.")]
    [SerializeField] private Light _sun;

    [Tooltip("Fallback sun rotation (euler) used when no Light is assigned.")]
    [SerializeField] private Vector3 _fallbackSunEuler = new Vector3(50f, -30f, 0f);

    [Header("Shadow Appearance")]
    [Tooltip("Material using Flynn/ProjectedShadow. Leave null to build one from the shader at runtime.")]
    [SerializeField] private Material _projectedMaterial;

    [SerializeField] private Color _shadowColor = new Color(0.16f, 0.21f, 0.32f, 0.55f);

    [Tooltip("How much the cast tip (head end) dissolves.")]
    [SerializeField, Range(0f, 1f)] private float _tipFade = 0.35f;

    [Tooltip("Silhouette alpha threshold.")]
    [SerializeField, Range(0f, 1f)] private float _alphaCutoff = 0.1f;

    [Tooltip("UV blur radius at the cast tip — penumbra widens with distance from the contact point.")]
    [SerializeField, Range(0f, 0.05f)] private float _penumbraScale = 0.012f;

    [Tooltip("Extra opacity at the contact end so objects feel grounded.")]
    [SerializeField, Range(0f, 1f)] private float _contactBoost = 0.35f;

    [SerializeField] private string _shadowSortingLayer = "Default";

    [Tooltip("Sorting order for shadows (should be just above the ground sprite, below props).")]
    [SerializeField] private int _shadowSortingOrder = 1;

    [Header("Length (sun elevation)")]
    [Tooltip("Master multiplier on the elevation-derived shadow length.")]
    [SerializeField] private float _lengthScale = 1f;

    [Tooltip("Clamp on the stretch factor along the sun-away direction.")]
    [SerializeField] private float _minLength = 0.6f;
    [SerializeField] private float _maxLength = 3.5f;

    [Header("Opacity (height fade)")]
    [Tooltip("Opacity at ground level (0-1).")]
    [SerializeField, Range(0f, 1f)] private float _groundOpacity = 1f;

    [Tooltip("Opacity at max height (0-1).")]
    [SerializeField, Range(0f, 1f)] private float _maxHeightOpacity = 0.3f;

    [Tooltip("Height at which opacity reaches _maxHeightOpacity.")]
    [SerializeField] private float _opacityFadeHeight = 5f;

    // ── Internal ────────────────────────────────────────────────────────────

    private readonly List<IShadowTarget> _targets = new();
    private readonly Dictionary<IShadowTarget, ShadowEntry> _entries = new();
    private readonly Stack<ShadowEntry> _pool = new();

    private Material _runtimeMaterial;
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ShearAmtId = Shader.PropertyToID("_ShearAmt");
    private static readonly int LatScaleId = Shader.PropertyToID("_LatScale");
    private static readonly int SpriteVMinId = Shader.PropertyToID("_SpriteVMin");
    private static readonly int SpriteVInvHId = Shader.PropertyToID("_SpriteVInvH");
    private static readonly int ShadowStretchId = Shader.PropertyToID("_ShadowStretch");

    private const string ShadowObjName = "_shadow";

    // ── Registration ────────────────────────────────────────────────────────

    /// <summary>Register a target for shadow rendering.</summary>
    public void Register(IShadowTarget target)
    {
        if (target == null) return;
        if (_targets.Contains(target)) return;

        _targets.Add(target);
        _entries[target] = GetOrCreateEntry();
    }

    /// <summary>Unregister a target from shadow rendering.</summary>
    public void Unregister(IShadowTarget target)
    {
        if (target == null) return;

        _targets.Remove(target);

        if (_entries.TryGetValue(target, out var entry))
        {
            _entries.Remove(target);
            entry.Anchor.gameObject.SetActive(false);
            _pool.Push(entry);
        }
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Pick up any targets already in the scene (edit mode or play).
        var sceneTargets = FindObjectsOfType<ShadowTarget>();
        foreach (var t in sceneTargets)
            Register(t);

#if UNITY_EDITOR
        // [ExecuteInEditMode] LateUpdate only fires on repaint, so the edit-mode
        // preview (silhouette + live sun direction) lags. Drive it every editor frame.
        UnityEditor.EditorApplication.update += EditorTick;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (!Application.isPlaying && this != null)
            Tick();
    }
#endif

    private void LateUpdate()
    {
        if (!Application.isPlaying) return; // edit mode handled by EditorTick
        Tick();
    }

    private void Tick()
    {
        Vector3 forwardOnGround;
        float lengthFactor;
        ComputeSunProjection(out forwardOnGround, out lengthFactor);
        Quaternion flatRot = Quaternion.LookRotation(Vector3.up, forwardOnGround);

        // Shear terms: lateral offsets on a camera-facing sprite stay along
        // camera-right when cast, while height projects along the sun azimuth.
        // Decompose camera-right against the quad's ground axes (lateral p̂, away f̂).
        Vector3 lateralAxis = flatRot * Vector3.right; // quad local X on the ground
        Vector3 camRight = GetCameraRightOnGround(lateralAxis);
        float latScale = Vector3.Dot(camRight, lateralAxis);
        float shearAmt = Vector3.Dot(camRight, forwardOnGround) / Mathf.Max(lengthFactor, 1e-3f);

        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            var target = _targets[i];

            // Clean up destroyed targets
            if (target == null || target.TargetTransform == null)
            {
                if (target != null && _entries.TryGetValue(target, out var dead))
                {
                    _entries.Remove(target);
                    dead.Anchor.gameObject.SetActive(false);
                    _pool.Push(dead);
                }
                _targets.RemoveAt(i);
                continue;
            }

            if (!_entries.TryGetValue(target, out var entry)) continue;

            UpdateShadow(target, entry, flatRot, lengthFactor, latScale, shearAmt);
        }
    }

    // ── Sun projection ──────────────────────────────────────────────────────

    private void ComputeSunProjection(out Vector3 forwardOnGround, out float lengthFactor)
    {
        Vector3 sunDir = _sun != null
            ? _sun.transform.forward
            : Quaternion.Euler(_fallbackSunEuler) * Vector3.forward;

        Vector3 horizontal = new Vector3(sunDir.x, 0f, sunDir.z);
        float hMag = horizontal.magnitude;
        float down = Mathf.Max(-sunDir.y, 1e-3f);

        // Sun near-vertical → arbitrary but stable azimuth.
        forwardOnGround = hMag > 1e-3f ? horizontal / hMag : Vector3.forward;

        lengthFactor = Mathf.Clamp((hMag / down) * _lengthScale, _minLength, _maxLength);
    }

    /// <summary>Camera right vector projected onto the ground; falls back to the
    /// quad's own lateral axis (no shear) when no camera is available.</summary>
    private static Vector3 GetCameraRightOnGround(Vector3 fallback)
    {
        Camera cam = Camera.main;
#if UNITY_EDITOR
        if (cam == null && UnityEditor.SceneView.lastActiveSceneView != null)
            cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
        if (cam == null) return fallback;

        Vector3 r = cam.transform.right;
        r.y = 0f;
        return r.sqrMagnitude > 1e-6f ? r.normalized : fallback;
    }

    // ── Shadow Update ───────────────────────────────────────────────────────

    private void UpdateShadow(IShadowTarget target, ShadowEntry entry, Quaternion flatRot, float lengthFactor,
                              float latScale, float shearAmt)
    {
        Sprite sprite = target.CurrentSprite;
        if (sprite == null)
        {
            entry.Anchor.gameObject.SetActive(false);
            return;
        }
        if (!entry.Anchor.gameObject.activeSelf)
            entry.Anchor.gameObject.SetActive(true);

        Bounds bounds = target.RendererBounds;

        float groundY = _groundY;
        if (_useRaycast)
        {
            var ray = new Ray(target.TargetTransform.position + Vector3.up * 0.1f, Vector3.down);
            if (Physics.Raycast(ray, out var hit, _raycastMaxDist, _groundLayer))
                groundY = hit.point.y;
        }

        // Anchor at the feet contact point on the ground.
        entry.Anchor.position = new Vector3(bounds.center.x, groundY, bounds.center.z);
        entry.Anchor.rotation = flatRot;
        entry.Anchor.localScale = new Vector3(target.ShadowScale, lengthFactor * target.ShadowScale, 1f);

        // Live sprite drives the silhouette; offset so the sprite's bottom sits at the
        // anchor origin (feet) — anchor scale then stretches the head away, feet fixed.
        var sr = entry.Renderer;
        sr.sprite = sprite;
        sr.flipX = target.FlipX;
        sr.sortingLayerName = _shadowSortingLayer;
        sr.sortingOrder = _shadowSortingOrder;
        Bounds sb = sprite.bounds;
        entry.Renderer.transform.localPosition = new Vector3(-sb.center.x, -sb.min.y, 0f);

        // Opacity fades as the object lifts off the ground.
        float height = Mathf.Max(bounds.min.y - groundY, 0f);
        float t01 = Mathf.Clamp01(height / _opacityFadeHeight);
        float opacity = Mathf.Lerp(_groundOpacity, _maxHeightOpacity, t01);

        // Atlas-safe base→tip coordinate for the shader's tip fade / penumbra.
        Texture2D tex = sprite.texture;
        Rect texRect = sprite.textureRect;
        float vMin = tex != null ? texRect.yMin / tex.height : 0f;
        float vH = tex != null ? texRect.height / tex.height : 1f;

        sr.GetPropertyBlock(entry.Mpb);
        entry.Mpb.SetFloat(OpacityId, opacity);
        entry.Mpb.SetFloat(LatScaleId, latScale);
        entry.Mpb.SetFloat(ShearAmtId, shearAmt);
        entry.Mpb.SetFloat(SpriteVMinId, vMin);
        entry.Mpb.SetFloat(SpriteVInvHId, vH > 1e-5f ? 1f / vH : 1f);
        entry.Mpb.SetFloat(ShadowStretchId, lengthFactor);
        sr.SetPropertyBlock(entry.Mpb);
    }

    // ── Pool ─────────────────────────────────────────────────────────────────

    private ShadowEntry GetOrCreateEntry()
    {
        if (_pool.Count > 0)
        {
            var pooled = _pool.Pop();
            pooled.Anchor.gameObject.SetActive(true);
            return pooled;
        }

        var anchor = new GameObject(ShadowObjName);
        anchor.transform.SetParent(transform, false);
        anchor.hideFlags = HideFlags.HideAndDontSave;

        var spriteGo = new GameObject("sprite");
        spriteGo.transform.SetParent(anchor.transform, false);
        spriteGo.hideFlags = HideFlags.HideAndDontSave;

        var sr = spriteGo.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = GetShadowMaterial();

        return new ShadowEntry
        {
            Anchor = anchor.transform,
            Renderer = sr,
            Mpb = new MaterialPropertyBlock()
        };
    }

    private Material GetShadowMaterial()
    {
        if (_projectedMaterial != null)
        {
            ApplyMaterialConfig(_projectedMaterial);
            return _projectedMaterial;
        }

        if (_runtimeMaterial == null)
        {
            var shader = Shader.Find("Flynn/ProjectedShadow");
            _runtimeMaterial = new Material(shader)
            {
                name = "ProjectedShadow (runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        ApplyMaterialConfig(_runtimeMaterial);
        return _runtimeMaterial;
    }

    private void ApplyMaterialConfig(Material mat)
    {
        mat.SetColor("_ShadowColor", _shadowColor);
        mat.SetFloat("_TipFade", _tipFade);
        mat.SetFloat("_AlphaCutoff", _alphaCutoff);
        mat.SetFloat("_PenumbraScale", _penumbraScale);
        mat.SetFloat("_ContactBoost", _contactBoost);
    }

    // ── Cleanup ─────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        foreach (var entry in _entries.Values)
            DestroyAnchor(entry);

        while (_pool.Count > 0)
            DestroyAnchor(_pool.Pop());

        _entries.Clear();
        _targets.Clear();
        _pool.Clear();

        if (_runtimeMaterial != null)
            DestroyImmediate(_runtimeMaterial);
    }

    private static void DestroyAnchor(ShadowEntry entry)
    {
        if (entry.Anchor != null && entry.Anchor.gameObject != null)
            DestroyImmediate(entry.Anchor.gameObject);
    }

    // ── Data ────────────────────────────────────────────────────────────────

    private struct ShadowEntry
    {
        public Transform Anchor;
        public SpriteRenderer Renderer;
        public MaterialPropertyBlock Mpb;
    }
}
