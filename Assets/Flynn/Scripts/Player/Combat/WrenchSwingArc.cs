using UnityEngine;

/// <summary>
/// Placeholder wrench swing VFX: a slash line that draws from the player toward
/// the hit point, plus an expanding ring at the endpoint. Both fade out.
/// Will be swapped for real sprite anims later.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class WrenchSwingArc : MonoBehaviour
{
    [Header("Slash Line")]
    [SerializeField] private float _slashDrawTime = 0.1f;
    [SerializeField] private float _slashFadeTime = 0.15f;
    [SerializeField] private float _slashWidth = 0.1f;

    [Header("Impact Ring")]
    [SerializeField] private float _ringMaxRadius = 0.5f;
    [SerializeField] private float _ringExpandTime = 0.1f;
    [SerializeField] private float _ringFadeTime = 0.2f;
    [SerializeField] private int _ringSegments = 24;

    [Header("Common")]
    [SerializeField] private float _arcHeight = 0.8f;
    [SerializeField] private Color _slashColor = new Color(1f, 0.9f, 0.6f);
    [SerializeField] private Color _ringColor = new Color(1f, 0.7f, 0.3f);

    private LineRenderer _slashLr;
    private LineRenderer _ringLr;
    private bool _playing;
    private float _timer;
    private Vector3 _startPos;
    private Vector3 _hitPos;

    private void Awake()
    {
        // Slash LineRenderer is configured in the inspector — only set runtime properties
        _slashLr = GetComponent<LineRenderer>();
        _slashLr.useWorldSpace = true;
        _slashLr.positionCount = 0;
        _slashLr.enabled = false;

        // Ring is created at runtime — needs full setup
        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(transform, false);
        _ringLr = ringGo.AddComponent<LineRenderer>();
        _ringLr.useWorldSpace = true;
        _ringLr.startWidth = _slashWidth * 0.6f;
        _ringLr.endWidth = _slashWidth * 0.2f;
        _ringLr.positionCount = 0;
        _ringLr.enabled = false;
        _ringLr.numCornerVertices = 4;
        _ringLr.numCapVertices = 4;
        _ringLr.loop = false;
        _ringLr.material = new Material(Shader.Find("Sprites/Default"));

        HideAll();
    }

    public void Play(Vector3 worldAimPoint)
    {
        _startPos = transform.position + Vector3.up * _arcHeight;
        _hitPos = worldAimPoint;

        _timer = 0f;
        _playing = true;
    }

    private void Update()
    {
        if (!_playing) return;

        _timer += Time.deltaTime;

        float slashTotal = _slashDrawTime + _slashFadeTime;
        float ringTotal = _ringExpandTime + _ringFadeTime;
        float total = Mathf.Max(slashTotal, ringTotal);

        if (_timer >= total)
        {
            _playing = false;
            HideAll();
            return;
        }

        UpdateSlash();
        UpdateRing();
    }

    private void UpdateSlash()
    {
        float drawT = Mathf.Clamp01(_timer / _slashDrawTime);

        float alpha = _timer <= _slashDrawTime
            ? 1f
            : Mathf.Clamp01(1f - (_timer - _slashDrawTime) / _slashFadeTime);

        if (alpha <= 0f) { _slashLr.enabled = false; return; }

        Vector3 tip = Vector3.Lerp(_startPos, _hitPos, drawT);
        _slashLr.enabled = true;
        _slashLr.positionCount = 2;
        _slashLr.SetPosition(0, _startPos);
        _slashLr.SetPosition(1, tip);

        Color c = _slashColor;
        _slashLr.startColor = new Color(c.r, c.g, c.b, alpha);
        _slashLr.endColor = new Color(c.r, c.g, c.b, alpha * 0.3f);
    }

    private void UpdateRing()
    {
        float ringStart = _slashDrawTime * 0.5f;
        float localT = _timer - ringStart;
        if (localT < 0f) { _ringLr.enabled = false; return; }

        float expandT = Mathf.Clamp01(localT / _ringExpandTime);

        float alpha = localT <= _ringExpandTime
            ? 1f
            : Mathf.Clamp01(1f - (localT - _ringExpandTime) / _ringFadeTime);

        if (alpha <= 0f) { _ringLr.enabled = false; return; }

        float radius = Mathf.Lerp(0.05f, _ringMaxRadius, expandT);

        _ringLr.enabled = true;
        _ringLr.positionCount = _ringSegments + 1;
        for (int i = 0; i <= _ringSegments; i++)
        {
            float angle = (i / (float)_ringSegments) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            _ringLr.SetPosition(i, _hitPos + offset);
        }

        Color c = _ringColor;
        _ringLr.startColor = new Color(c.r, c.g, c.b, alpha);
        _ringLr.endColor = new Color(c.r, c.g, c.b, alpha * 0.5f);
    }

    private void HideAll()
    {
        _slashLr.enabled = false;
        _ringLr.enabled = false;
    }
}
