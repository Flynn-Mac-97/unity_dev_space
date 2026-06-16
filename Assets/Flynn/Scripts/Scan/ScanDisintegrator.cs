using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a disintegration animation when the attached <see cref="ScanTarget"/> completes.
/// The sprite shatters into small fragments that scatter outward with spin and gravity,
/// each fading to nothing. A bright flash precedes the break for dramatic impact.
/// No custom shaders required — pure Unity sprite manipulation.
/// </summary>
[RequireComponent(typeof(ScanTarget))]
public class ScanDisintegrator : MonoBehaviour
{
    [Header("Fragment")]
    [Tooltip("How many columns/rows to slice the sprite into.")]
    [SerializeField] private int _gridSize = 6;
    [SerializeField] private float _scatterForce = 3f;
    [SerializeField] private float _upForce = 2.5f;
    [SerializeField] private float _spinSpeed = 360f;
    [SerializeField] private float _fragmentLifetime = 1.2f;
    [SerializeField] private float _fragmentFadeStart = 0.3f;

    [Header("Flash")]
    [SerializeField] private float _flashDuration = 0.15f;
    [SerializeField] private float _flashIntensity = 2.5f;

    private ScanTarget _target;
    private SpriteRenderer _spriteRenderer;
    private bool _triggered;

    private void Awake()
    {
        _target = GetComponent<ScanTarget>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()  { _target.OnScanComplete += HandleComplete; }
    private void OnDisable() { if (_target != null) _target.OnScanComplete -= HandleComplete; }

    private void HandleComplete()
    {
        if (_triggered) return;
        _triggered = true;
        StartCoroutine(DisintegrateRoutine());
    }

    private IEnumerator DisintegrateRoutine()
    {
        // Disable colliders immediately so the object can't be interacted with.
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        // Stop the billboard from overwriting fragment rotations.
        Billboard billboard = GetComponentInChildren<Billboard>();
        if (billboard != null) billboard.enabled = false;

        // Brief white flash on the original sprite.
        if (_spriteRenderer != null)
        {
            Color baseColor = _spriteRenderer.color;
            _spriteRenderer.color = new Color(
                Mathf.Min(1f, baseColor.r * _flashIntensity),
                Mathf.Min(1f, baseColor.g * _flashIntensity),
                Mathf.Min(1f, baseColor.b * _flashIntensity),
                baseColor.a);
            yield return new WaitForSeconds(_flashDuration);
            _spriteRenderer.enabled = false;
        }

        // Spawn fragments.
        Sprite sprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
        if (sprite == null) { Destroy(gameObject); yield break; }

        Transform visualParent = _spriteRenderer.transform;
        Vector3 origin = visualParent.position;
        Quaternion originRot = visualParent.rotation;
        Vector3 lossyScale = visualParent.lossyScale;

        // Compute sprite bounds in world space.
        Bounds bounds = sprite.bounds;
        float halfW = bounds.extents.x;
        float halfH = bounds.extents.y;

        FragmentData[] fragments = new FragmentData[_gridSize * _gridSize];
        int idx = 0;

        for (int row = 0; row < _gridSize; row++)
        {
            for (int col = 0; col < _gridSize; col++)
            {
                float uMin = (float)col / _gridSize;
                float uMax = (float)(col + 1) / _gridSize;
                float vMin = (float)row / _gridSize;
                float vMax = (float)(row + 1) / _gridSize;

                // Centre of this fragment in local sprite space.
                float cx = Mathf.Lerp(-halfW, halfW, (uMin + uMax) * 0.5f);
                float cy = Mathf.Lerp(-halfH, halfH, (vMin + vMax) * 0.5f);

                // World position (apply the visual parent's transform).
                Vector3 fragPos = origin + originRot * Vector3.Scale(new Vector3(cx, cy, 0f), lossyScale);

                GameObject fragGo = new GameObject($"Frag_{col}_{row}");
                fragGo.transform.position = fragPos;
                fragGo.transform.rotation = originRot;
                fragGo.transform.localScale = lossyScale;

                SpriteRenderer fragSr = fragGo.AddComponent<SpriteRenderer>();
                fragSr.sprite = sprite;
                fragSr.material = _spriteRenderer.sharedMaterial;

                // Copy the base colour (post-flash we want the original look).
                Color fragColor = _spriteRenderer.color;
                fragColor.a = 1f;
                fragSr.color = fragColor;

                // Use the texture rect to calculate UV sub-rect.
                Rect texRect = sprite.textureRect;
                float texW = sprite.texture.width;
                float texH = sprite.texture.height;
                float pxMin = (texRect.xMin + uMin * texRect.width) / texW;
                float pxMax = (texRect.xMin + uMax * texRect.width) / texW;
                float pyMin = (texRect.yMin + vMin * texRect.height) / texH;
                float pyMax = (texRect.yMin + vMax * texRect.height) / texH;

                // Override the sprite with a sub-section by creating a temporary sprite.
                Sprite subSprite = Sprite.Create(
                    sprite.texture,
                    new Rect(
                        texRect.xMin + uMin * texRect.width,
                        texRect.yMin + vMin * texRect.height,
                        (uMax - uMin) * texRect.width,
                        (vMax - vMin) * texRect.height),
                    new Vector2(0.5f, 0.5f),
                    sprite.pixelsPerUnit);
                fragSr.sprite = subSprite;
                fragSr.sortingLayerID = _spriteRenderer.sortingLayerID;
                fragSr.sortingOrder = _spriteRenderer.sortingOrder + 1;

                // Scatter direction: outward from the object centre + random.
                Vector3 outward = (fragPos - origin).normalized;
                Vector3 randomDir = new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(0.5f, 1f),
                    Random.Range(-0.3f, 0.3f));
                Vector3 vel = (outward * _scatterForce + randomDir * _scatterForce * 0.5f + Vector3.up * _upForce);

                float spin = Random.Range(-_spinSpeed, _spinSpeed);

                fragments[idx++] = new FragmentData
                {
                    transform = fragGo.transform,
                    velocity = vel,
                    spin = spin,
                    renderer = fragSr,
                    baseColor = fragColor,
                    delay = Random.Range(0f, 0.08f) // slight stagger
                };
            }
        }

        // Animate fragments.
        float t = 0f;
        while (t < _fragmentLifetime)
        {
            t += Time.deltaTime;
            for (int i = 0; i < fragments.Length; i++)
            {
                float ft = t - fragments[i].delay;
                if (ft < 0f) continue;

                // Gravity.
                var vel = fragments[i].velocity;
                vel += Physics.gravity * Time.deltaTime;
                fragments[i].velocity = vel;
                fragments[i].transform.position += fragments[i].velocity * Time.deltaTime;
                fragments[i].transform.Rotate(Vector3.forward, fragments[i].spin * Time.deltaTime);

                // Fade.
                if (ft > _fragmentFadeStart)
                {
                    float fadeT = (ft - _fragmentFadeStart) / (_fragmentLifetime - _fragmentFadeStart);
                    Color c = fragments[i].baseColor;
                    c.a = Mathf.Lerp(1f, 0f, fadeT);
                    fragments[i].renderer.color = c;
                }
            }
            yield return null;
        }

        // Cleanup.
        foreach (var frag in fragments)
        {
            if (frag.transform != null)
                Destroy(frag.transform.gameObject);
        }

        Destroy(gameObject);
    }

    private struct FragmentData
    {
        public Transform transform;
        public Vector3 velocity;
        public float spin;
        public SpriteRenderer renderer;
        public Color baseColor;
        public float delay;
    }
}
