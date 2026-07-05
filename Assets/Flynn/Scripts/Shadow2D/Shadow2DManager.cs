using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Flynn.Shadow2D
{
    /// <summary>
    /// Scene-level manager for ProjectedShadow2D. Discovers all Shadow2DCaster
    /// components, creates shadow sprite children behind each caster, and updates
    /// per-frame shadow direction and stretch via MaterialPropertyBlock.
    ///
    /// This system works with Global Light2D (which URP ShadowCaster2D cannot do)
    /// by using a configurable shadow direction and stretch instead of relying on
    /// light-based shadow masking.
    ///
    /// One per scene on the MANAGERS object.
    /// Runs in edit mode too so shadows are visible while composing scenes.
    /// </summary>
    [ExecuteAlways]
    public class Shadow2DManager : MonoBehaviour
    {
        [Header("Light")]
        [Tooltip("Light2D used for per-caster shadow direction. If null or Global, uses _fixedShadowDir.")]
        [SerializeField] private Light2D _shadowLight;

        [Header("Shadow Direction")]
        [Tooltip("Fixed shadow direction (XY). Used when light is null or Global. Normalized at runtime.")]
        [SerializeField] private Vector2 _fixedShadowDir = new Vector2(0.25f, -0.35f);

        [Header("Shadow Shape")]
        [Tooltip("How far the shadow stretches in the shadow direction (object-space units).")]
        [SerializeField] private float _shadowStretch = 0.5f;

        [Tooltip("Raises the shadow anchor above the sprite's lowest pixel, as a fraction " +
                 "of sprite height. Iso art's ground contact is the footprint centre, not " +
                 "the front corner — without this lift objects look like they float.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _baseLift = 0.18f;

        [Header("Material")]
        [Tooltip("Material using the Flynn/ProjectedShadow2D shader. If null, loads from Resources.")]
        [SerializeField] private Material _shadowMaterial;

        [Header("Sorting")]
        [Tooltip("Sorting order offset relative to the caster. Negative = behind.")]
        [SerializeField] private int _sortingOrderOffset = -1;

        // ── Internals ──────────────────────────────────────────────────────────

        private struct ShadowEntry
        {
            public Shadow2DCaster Caster;
            public SpriteRenderer CasterSR;
            public SpriteRenderer ShadowSR;
            public MaterialPropertyBlock MPB;
        }

        private readonly List<ShadowEntry> _entries = new();

        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int PropShear  = Shader.PropertyToID("_ShearX");
        private static readonly int PropSquash = Shader.PropertyToID("_SquashY");
        private static readonly int PropBaseY  = Shader.PropertyToID("_BaseY");
        private static readonly int PropVMin   = Shader.PropertyToID("_SpriteVMin");
        private static readonly int PropVInvH  = Shader.PropertyToID("_SpriteVInvH");

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_shadowMaterial == null)
            {
                var shader = Shader.Find("Flynn/ProjectedShadow2D");
                if (shader != null)
                {
                    // DontSave: a runtime-created material must never be serialized
                    // into the scene, or every Shadow2D child saves a null material
                    // and renders magenta on the next load.
                    _shadowMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }

                if (_shadowMaterial == null)
                    Debug.LogError("[Shadow2DManager] Flynn/ProjectedShadow2D shader not found. " +
                                   "Assign _shadowMaterial manually.");
            }

            // If no light assigned, try to find a non-global Light2D
            if (_shadowLight == null)
            {
                var lights = FindObjectsOfType<Light2D>();
                foreach (var l in lights)
                {
                    if (l.lightType != Light2D.LightType.Global)
                    {
                        _shadowLight = l;
                        break;
                    }
                }
            }

            RefreshCasters();
        }

        private void LateUpdate()
        {
            bool useLightPos = _shadowLight != null
                && _shadowLight.lightType != Light2D.LightType.Global;

            Vector2 lightPos = useLightPos
                ? (Vector2)_shadowLight.transform.position
                : Vector2.zero;

            Vector2 fixedDir = _fixedShadowDir.normalized;
            if (fixedDir.sqrMagnitude < 0.001f) fixedDir = new Vector2(0.25f, -0.35f).normalized;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];

                // Cleanup destroyed casters
                if (e.Caster == null || e.CasterSR == null)
                {
                    if (e.ShadowSR != null && e.ShadowSR.gameObject != null)
                    {
                        if (Application.isPlaying) Destroy(e.ShadowSR.gameObject);
                        else DestroyImmediate(e.ShadowSR.gameObject);
                    }
                    _entries.RemoveAt(i);
                    continue;
                }

                // Sync sprite (handles animation / sprite swaps)
                if (e.ShadowSR.sprite != e.CasterSR.sprite)
                    e.ShadowSR.sprite = e.CasterSR.sprite;

                // Sync sorting
                e.ShadowSR.sortingLayerName = e.CasterSR.sortingLayerName;
                e.ShadowSR.sortingOrder = e.CasterSR.sortingOrder + _sortingOrderOffset;

                // Compute shadow direction in world space, then convert to object space
                Vector2 worldDir;
                if (useLightPos)
                {
                    Vector2 casterPos = e.CasterSR.transform.position;
                    worldDir = (casterPos - lightPos).normalized;
                    if (worldDir.sqrMagnitude < 0.001f) worldDir = fixedDir;
                }
                else
                {
                    worldDir = fixedDir;
                }

                // Transform to object space so the shear is correct regardless of
                // caster rotation/flip.
                Vector2 objDir = e.CasterSR.transform.InverseTransformDirection(worldDir);

                // Sun-shadow mapping: a feature at height H above the sprite base
                // lands at (shear*H, base - squash*H). Length scales with stretch.
                float len = _shadowStretch * e.Caster.StretchMultiplier;
                float shear = objDir.x * len;
                float squash = Mathf.Max(0.05f, -objDir.y * len);

                // Sprite base + V mapping (atlas-safe)
                float vMin = 0f, vInvH = 1f, baseY = 0f;
                var sprite = e.CasterSR.sprite;
                if (sprite != null && sprite.texture != null)
                {
                    baseY = sprite.bounds.min.y + sprite.bounds.size.y * _baseLift;
                    float texH = sprite.texture.height;
                    if (texH > 0)
                    {
                        vMin = sprite.textureRect.yMin / texH;
                        float vHeight = sprite.textureRect.height / texH;
                        vInvH = vHeight > 0.0001f ? 1f / vHeight : 1f;
                    }
                }

                // Update MaterialPropertyBlock. Read the renderer's block first and
                // bind the sprite texture explicitly — a custom MPB otherwise stomps
                // the SpriteRenderer-provided _MainTex and the shader samples the
                // material's white default (block shadow instead of alpha silhouette).
                e.ShadowSR.GetPropertyBlock(e.MPB);
                if (sprite != null && sprite.texture != null)
                    e.MPB.SetTexture(PropMainTex, sprite.texture);
                e.MPB.SetFloat(PropShear, shear);
                e.MPB.SetFloat(PropSquash, squash);
                e.MPB.SetFloat(PropBaseY, baseY);
                e.MPB.SetFloat(PropVMin, vMin);
                e.MPB.SetFloat(PropVInvH, vInvH);
                e.ShadowSR.SetPropertyBlock(e.MPB);
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Discovers all Shadow2DCaster components in the scene and creates
        /// shadow sprite children for any that don't have one yet.
        /// Call this after spawning new casters at runtime.
        /// </summary>
        public void RefreshCasters()
        {
            var casters = FindObjectsOfType<Shadow2DCaster>();

            foreach (var caster in casters)
            {
                bool found = false;
                foreach (var e in _entries)
                {
                    if (ReferenceEquals(e.Caster, caster)) { found = true; break; }
                }
                if (found) continue;

                AddCaster(caster);
            }

            Debug.Log($"[Shadow2DManager] Tracking {_entries.Count} shadow casters.");
        }

        /// <summary>Registers a single caster at runtime (e.g. spawned prefab).</summary>
        public void Register(Shadow2DCaster caster)
        {
            foreach (var e in _entries)
            {
                if (ReferenceEquals(e.Caster, caster)) return;
            }
            AddCaster(caster);
        }

        // ── Internal ───────────────────────────────────────────────────────────

        private void AddCaster(Shadow2DCaster caster)
        {
            var casterSR = caster.GetComponent<SpriteRenderer>();
            if (casterSR == null || casterSR.sprite == null)
            {
                Debug.LogWarning($"[Shadow2DManager] {caster.name} has no SpriteRenderer or sprite, skipping.");
                return;
            }

            // Reuse a previously saved shadow child if one exists — creating a
            // second one would stack shadows and orphan the old (possibly
            // material-less) copy.
            var existing = caster.transform.Find("Shadow2D");
            var shadowGO = existing != null ? existing.gameObject : new GameObject("Shadow2D");
            shadowGO.transform.SetParent(caster.transform, false);
            shadowGO.transform.localPosition = Vector3.zero;
            shadowGO.transform.localRotation = Quaternion.identity;
            shadowGO.transform.localScale = Vector3.one;

            var shadowSR = shadowGO.GetComponent<SpriteRenderer>();
            if (shadowSR == null) shadowSR = shadowGO.AddComponent<SpriteRenderer>();
            shadowSR.sprite = casterSR.sprite;
            shadowSR.sharedMaterial = _shadowMaterial;
            shadowSR.sortingLayerName = casterSR.sortingLayerName;
            shadowSR.sortingOrder = casterSR.sortingOrder + _sortingOrderOffset;
            shadowSR.color = Color.white;

            var mpb = new MaterialPropertyBlock();
            shadowSR.SetPropertyBlock(mpb);

            _entries.Add(new ShadowEntry
            {
                Caster = caster,
                CasterSR = casterSR,
                ShadowSR = shadowSR,
                MPB = mpb
            });
        }
    }
}
