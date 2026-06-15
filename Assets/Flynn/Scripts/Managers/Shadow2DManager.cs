using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Manages 2D sprite silhouette shadows for pure XY-plane scenes.
/// Each registered Shadow2DTarget gets a pooled shadow sprite that is offset
/// and stretched away from a light source (Global Light2D or manual direction),
/// rendered with the Flynn/ProjectedShadow2D shader as a dark silhouette.
/// </summary>
[DefaultExecutionOrder(-89)]
[ExecuteInEditMode]
public class Shadow2DManager : MonoBehaviour
{
    // ── Config ──────────────────────────────────────────────────────────────

    [Header("Light Source")]
    [Tooltip("Light2D to derive shadow direction from. Should be a Global or Point light. Leave null to use manual direction.")]
    [SerializeField] private Light2D _lightSource;

    [Tooltip("Manual light direction when no Light2D is assigned. Shadows cast opposite this direction.")]
    [SerializeField] private Vector2 _manualLightDir = new Vector2(-0.5f, -1f);

    [Header("Shadow Appearance")]
    [Tooltip("Material using Flynn/ProjectedShadow2D. Leave null to build one at runtime.")]
    [SerializeField] private Material _shadowMaterial;

    [SerializeField] private Color _shadowColor = new Color(0.1f, 0.14f, 0.22f, 0.5f);

    [Tooltip("How much shadows stretch along the cast direction.")]
    [SerializeField, Range(0.1f, 5f)] private float _stretchScale = 1f;

    [Tooltip("Minimum shadow length (keeps shadows visible even with overhead light).")]
    [SerializeField, Range(0f, 2f)] private float _minStretch = 0.3f;

    [Tooltip("Maximum shadow length.")]
    [SerializeField, Range(0.5f, 8f)] private float _maxStretch = 4f;

    [Tooltip("Shadow offset from the caster origin along the cast direction.")]
    [SerializeField, Range(0f, 2f)] private float _baseOffset = 0.1f;

    [SerializeField] private string _sortingLayer = "Default";
    [SerializeField] private int _sortingOrder = -5;

    // ── Internal ────────────────────────────────────────────────────────────

    private readonly List<Shadow2DTarget> _targets = new();
    private readonly Dictionary<Shadow2DTarget, ShadowEntry> _entries = new();
    private readonly Stack<ShadowEntry> _pool = new();

    private Material _runtimeMaterial;
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ShadowDirXId = Shader.PropertyToID("_ShadowDirX");
    private static readonly int ShadowDirYId = Shader.PropertyToID("_ShadowDirY");
    private static readonly int ShadowStretchId = Shader.PropertyToID("_ShadowStretch");
    private static readonly int SpriteVMinId = Shader.PropertyToID("_SpriteVMin");
    private static readonly int SpriteVInvHId = Shader.PropertyToID("_SpriteVInvH");

    private const string ShadowObjName = "_shadow2d";

    // ── Registration ────────────────────────────────────────────────────────

    public void Register(Shadow2DTarget target)
    {
        if (target == null || _targets.Contains(target)) return;
        _targets.Add(target);
        _entries[target] = GetOrCreateEntry();
    }

    public void Unregister(Shadow2DTarget target)
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
        var sceneTargets = FindObjectsOfType<Shadow2DTarget>();
        foreach (var t in sceneTargets)
            Register(t);

#if UNITY_EDITOR
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
        Tick();
    }

    private void Tick()
    {
        // Keep the shared material config in sync with Inspector changes
        var mat = GetShadowMaterial();
        ApplyMaterialConfig(mat);

        Vector2 lightDir = GetLightDirection();
        // Shadow casts opposite to light direction
        Vector2 shadowDir = -lightDir.normalized;

        // Stretch based on how horizontal the light is (more horizontal = longer shadow)
        float horizontalness = Mathf.Abs(lightDir.x) / Mathf.Max(Mathf.Abs(lightDir.x) + Mathf.Abs(lightDir.y), 0.01f);
        float stretch = Mathf.Clamp(horizontalness * _stretchScale * 3f, _minStretch, _maxStretch);

        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            var target = _targets[i];
            if (target == null || target.transform == null)
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
            // Ensure the material hasn't been swapped (e.g. by a test script)
            if (entry.Renderer.sharedMaterial != mat)
                entry.Renderer.sharedMaterial = mat;
            UpdateShadow(target, entry, shadowDir, stretch);
        }
    }

    // ── Light Direction ─────────────────────────────────────────────────────

    private Vector2 GetLightDirection()
    {
        if (_lightSource != null)
        {
            // A Global Light2D carries no positional/directional data of its own,
            // but its transform does. Treat the light GameObject's local -up axis
            // as the "sun ray" direction so rotating the sun rotates all shadows.
            // (Light points down its local -up; shadows cast opposite, see Tick.)
            Vector2 dir = -(Vector2)_lightSource.transform.up;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : _manualLightDir.normalized;
        }
        return _manualLightDir.normalized;
    }

    // ── Shadow Update ───────────────────────────────────────────────────────

    private void UpdateShadow(Shadow2DTarget target, ShadowEntry entry, Vector2 shadowDir, float stretch)
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

        // Check for floating component — modifies lift, opacity, stretch, and spread
        var floating = target.Floating;

        // Position the shadow anchor at the base of the object, offset in shadow direction
        float offset = _baseOffset + bounds.extents.y * 0.1f;
        Vector3 pos = target.TargetTransform.position;
        pos.x += shadowDir.x * offset;
        pos.y += shadowDir.y * offset;
        // Lift: shift shadow downward for floating/overhanging objects
        pos.y -= target.ShadowLift;
        if (floating != null)
            pos.y -= floating.FloatHeight;
        pos.z = -0.01f; // toward a +Z-looking 2D ortho camera (in front of the z=0 ground plane)
        entry.Anchor.position = pos;

        var sr = entry.Renderer;
        sr.sprite = sprite;
        sr.flipX = target.FlipX;
        sr.sortingLayerName = _sortingLayer;
        sr.sortingOrder = _sortingOrder;

        // Scale: stretch along shadow direction via shader, keep normal scale otherwise
        // Floating objects get an extra spread scale to simulate diffusion
        float scaleMul = target.ShadowScale;
        if (floating != null)
            scaleMul *= floating.ExtraSpreadScale;
        entry.Anchor.localScale = new Vector3(scaleMul, scaleMul, 1f);

        // Atlas-safe base->tip coordinate for the shader's tip fade
        Texture2D tex = sprite.texture;
        Rect texRect = sprite.textureRect;
        float vMin = tex != null ? texRect.yMin / tex.height : 0f;
        float vH = tex != null ? texRect.height / tex.height : 1f;

        // Opacity: floating objects cast fainter shadows (light diffuses with distance)
        float opacity = 1f;
        if (floating != null)
            opacity *= floating.OpacityMultiplier;

        // Stretch: floating objects cast more stretched/diffuse shadows
        float totalStretch = stretch;
        if (floating != null)
            totalStretch += floating.ExtraStretch;

        sr.GetPropertyBlock(entry.Mpb);
        entry.Mpb.SetFloat(OpacityId, opacity);
        entry.Mpb.SetFloat(ShadowDirXId, shadowDir.x);
        entry.Mpb.SetFloat(ShadowDirYId, shadowDir.y);
        entry.Mpb.SetFloat(ShadowStretchId, totalStretch * scaleMul);
        entry.Mpb.SetFloat(SpriteVMinId, vMin);
        entry.Mpb.SetFloat(SpriteVInvHId, vH > 1e-5f ? 1f / vH : 1f);
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

        var sr = anchor.AddComponent<SpriteRenderer>();
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
        if (_shadowMaterial != null)
        {
            ApplyMaterialConfig(_shadowMaterial);
            return _shadowMaterial;
        }

        if (_runtimeMaterial == null)
        {
            var shader = Shader.Find("Flynn/ProjectedShadow2D");
            _runtimeMaterial = new Material(shader)
            {
                name = "ProjectedShadow2D (runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        ApplyMaterialConfig(_runtimeMaterial);
        return _runtimeMaterial;
    }

    private void ApplyMaterialConfig(Material mat)
    {
        mat.SetColor("_ShadowColor", _shadowColor);
    }

    // ── Gizmos ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private static readonly Color SunRayColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    private static readonly Color ShadowDirColor = new Color(0.4f, 0.5f, 1f, 0.7f);

    private void OnDrawGizmos()
    {
        if (_lightSource == null) return;

        Vector3 origin = _lightSource.transform.position;
        Vector2 lightDir2D = GetLightDirection();
        Vector2 shadowDir2D = -lightDir2D.normalized;

        // Draw sun ray direction (yellow)
        Gizmos.color = SunRayColor;
        Vector3 lightEnd = origin + new Vector3(lightDir2D.x, lightDir2D.y, 0) * 2f;
        Gizmos.DrawLine(origin, lightEnd);
        DrawArrowHead(origin, lightEnd, SunRayColor);

        // Draw shadow cast direction (blue)
        Gizmos.color = ShadowDirColor;
        Vector3 shadowEnd = origin + new Vector3(shadowDir2D.x, shadowDir2D.y, 0) * 1.5f;
        Gizmos.DrawLine(origin, shadowEnd);
        DrawArrowHead(origin, shadowEnd, ShadowDirColor);

        // Labels
        GUI.color = SunRayColor;
        UnityEditor.Handles.Label(lightEnd + Vector3.right * 0.3f, "Sun");
        GUI.color = ShadowDirColor;
        UnityEditor.Handles.Label(shadowEnd + Vector3.right * 0.3f, "Shadow");
        GUI.color = Color.white;
    }

    private static void DrawArrowHead(Vector3 from, Vector3 to, Color color)
    {
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.ArrowHandleCap(0, to, Quaternion.LookRotation(to - from), 0.3f, EventType.Repaint);
    }
#endif

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
