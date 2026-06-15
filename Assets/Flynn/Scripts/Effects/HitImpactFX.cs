using System.Collections;
using UnityEngine;

/// <summary>
/// Procedural hit-impact VFX: polished particle burst, expanding flash ring,
/// and floating damage number. Particles sort above the hit target.
/// All generated at runtime — no prefab assets required.
/// </summary>
public class HitImpactFX : MonoBehaviour
{
    [Header("Impact Particles")]
    [SerializeField] private int _particleCount = 14;
    [SerializeField] private float _particleSpeed = 5f;
    [SerializeField] private float _particleLife = 0.45f;
    [SerializeField] private float _particleMinSize = 0.06f;
    [SerializeField] private float _particleMaxSize = 0.14f;

    [Header("Flash Ring")]
    [SerializeField] private float _flashStartScale = 0.3f;
    [SerializeField] private float _flashEndScale = 1.4f;
    [SerializeField] private float _flashDuration = 0.12f;

    [Header("Damage Number")]
    [SerializeField] private float _numberDuration = 0.9f;
    [SerializeField] private float _numberRiseSpeed = 1.2f;

    [Header("Air Whoosh")]
    [SerializeField] private int _whooshParticleCount = 8;
    [SerializeField] private float _whooshSpeed = 3f;
    [SerializeField] private float _whooshLife = 0.25f;

    private static HitImpactFX _instance;
    public static HitImpactFX Instance => _instance;

    private void Awake() => _instance = this;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Full impact: particles + flash + damage number. Renders above the target's sorting order.</summary>
    public void PlayHit(Vector3 position, Vector3 direction, int damage, Color color, GameObject target = null)
    {
        int sortOrder = GetSortOrder(target) + 10;
        SpawnParticleBurst(position, direction, _particleCount, _particleSpeed, _particleLife, color, sortOrder);
        StartCoroutine(FlashRingRoutine(position, color, sortOrder + 1));
        if (damage > 0) SpawnDamageNumber(position, damage, color);
    }

    /// <summary>Light impact for non-resource surfaces (sparks / dust).</summary>
    public void PlaySurfaceHit(Vector3 position, Vector3 direction, GameObject target = null)
    {
        var dustColor = new Color(0.8f, 0.72f, 0.6f);
        int sortOrder = GetSortOrder(target) + 10;
        SpawnParticleBurst(position, direction, _particleCount / 2, _particleSpeed * 0.7f, _particleLife, dustColor, sortOrder);
        StartCoroutine(FlashRingRoutine(position, dustColor, sortOrder + 1));
    }

    /// <summary>Air whoosh on a swing that hits nothing.</summary>
    public void PlayAirWhoosh(Vector3 position, Vector3 direction)
    {
        var whooshColor = new Color(1f, 0.95f, 0.8f, 0.5f);
        SpawnParticleBurst(position, direction, _whooshParticleCount, _whooshSpeed, _whooshLife, whooshColor, 100);
    }

    // ── Particle burst ─────────────────────────────────────────────────────

    private void SpawnParticleBurst(Vector3 pos, Vector3 dir, int count, float speed, float life, Color color, int sortOrder)
    {
        for (int i = 0; i < count; i++)
        {
            float size = Random.Range(_particleMinSize, _particleMaxSize);
            var go = CreateQuad($"_hitP_{i}");
            go.transform.position = pos + Random.onUnitSphere * 0.05f;
            go.transform.localScale = Vector3.one * size;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.color = color;
            sr.sortingOrder = sortOrder;

            // Random rotation for visual variety
            go.transform.Rotate(0f, 0f, Random.Range(0f, 360f));

            StartCoroutine(ParticleRoutine(go, pos, dir, speed, life, size));
        }
    }

    private IEnumerator ParticleRoutine(GameObject go, Vector3 origin, Vector3 dir, float speed, float life, float baseSize)
    {
        float t = 0f;

        // Circular burst: spread in a cone facing away from hit direction
        Vector3 baseDir = dir.normalized;
        if (baseDir.sqrMagnitude < 0.01f) baseDir = Vector3.up;

        // Perpendicular vectors for creating the burst circle
        Vector3 perp1 = Vector3.Cross(baseDir, Vector3.up);
        if (perp1.sqrMagnitude < 0.01f) perp1 = Vector3.Cross(baseDir, Vector3.right);
        perp1.Normalize();
        Vector3 perp2 = Vector3.Cross(baseDir, perp1).normalized;

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float spreadAngle = Random.Range(20f, 60f) * Mathf.Deg2Rad;
        Vector3 burstDir = Quaternion.AngleAxis(spreadAngle * Mathf.Rad2Deg, perp1 * Mathf.Sin(angle) + perp2 * Mathf.Cos(angle)) * baseDir;

        float speedMul = Random.Range(0.5f, 1.3f);
        Vector3 vel = burstDir * speed * speedMul;
        vel.y += Random.Range(0.5f, 2.5f);

        float spinSpeed = Random.Range(-400f, 400f);

        var sr = go.GetComponent<SpriteRenderer>();
        while (t < life)
        {
            t += Time.unscaledDeltaTime;
            float p = t / life;

            go.transform.position += vel * Time.unscaledDeltaTime;
            vel.y -= 8f * Time.unscaledDeltaTime;
            vel *= 1f - 1.5f * Time.unscaledDeltaTime;

            go.transform.Rotate(0f, 0f, spinSpeed * Time.unscaledDeltaTime);

            var c = sr.color;
            c.a = 1f - p * p;
            sr.color = c;

            float scale = baseSize * (1f - p * 0.6f);
            go.transform.localScale = Vector3.one * scale;

            yield return null;
        }
        Destroy(go);
    }

    // ── Flash ring ──────────────────────────────────────────────────────────

    private IEnumerator FlashRingRoutine(Vector3 pos, Color color, int sortOrder)
    {
        var go = CreateQuad("_hitFlash");
        go.transform.position = pos + Vector3.up * 0.06f;
        go.transform.localScale = Vector3.one * _flashStartScale;

        var sr = go.GetComponent<SpriteRenderer>();
        sr.color = new Color(1f, 1f, 1f, 1f);
        sr.sortingOrder = sortOrder;

        float t = 0f;
        while (t < _flashDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / _flashDuration;
            float ease = 1f - (1f - p) * (1f - p);
            float scale = Mathf.Lerp(_flashStartScale, _flashEndScale, ease);
            go.transform.localScale = Vector3.one * scale;

            var c = sr.color;
            c.a = 1f - ease;
            sr.color = c;

            yield return null;
        }
        Destroy(go);
    }

    // ── Damage number ──────────────────────────────────────────────────────

    private void SpawnDamageNumber(Vector3 pos, int damage, Color color)
    {
        var go = new GameObject("_dmgNum");
        go.transform.position = pos + Vector3.up * 0.8f;

        var tm = go.AddComponent<TextMesh>();
        tm.text = damage.ToString();
        tm.fontSize = 48;
        tm.color = color;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.characterSize = 0.1f;
        var rend = go.GetComponent<MeshRenderer>();
        rend.sortingOrder = 500;

        StartCoroutine(DamageNumberRoutine(go, tm));
    }

    private IEnumerator DamageNumberRoutine(GameObject go, TextMesh tm)
    {
        float t = 0f;
        Vector3 basePos = go.transform.position;
        var cam = Camera.main;

        while (t < _numberDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / _numberDuration;

            basePos.y += _numberRiseSpeed * Time.unscaledDeltaTime;
            go.transform.position = basePos;

            // Face the camera
            if (cam != null)
                go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);

            var c = tm.color;
            c.a = 1f - p;
            tm.color = c;

            float scalePunch = 1f + 0.4f * Mathf.Sin(p * Mathf.PI);
            go.transform.localScale = Vector3.one * scalePunch;

            yield return null;
        }
        Destroy(go);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static int GetSortOrder(GameObject target)
    {
        if (target == null) return 0;
        var sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.sortingOrder;
        var mr = target.GetComponentInChildren<MeshRenderer>();
        if (mr != null) return mr.sortingOrder;
        return 0;
    }

    private static GameObject CreateQuad(string name)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.material = new Material(Shader.Find("Sprites/Default"));
        return go;
    }

    private static Sprite _whiteSprite;
    private static Sprite CreateWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _whiteSprite;
    }
}
