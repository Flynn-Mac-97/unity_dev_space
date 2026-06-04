using System.Collections;
using UnityEngine;

/// <summary>
/// Positional recoil on a <see cref="ResourceNode"/> when it's hit — a quick decaying nudge so the
/// node visibly rocks from the blow. Amplitude scales with the damage dealt (heavy swings rock it
/// harder). Nudges the node root's position only: the sprite's rotation is owned by
/// <see cref="Billboard"/>, so a positional shake reads cleanly without fighting it.
/// </summary>
[RequireComponent(typeof(ResourceNode))]
public class ResourceShake : MonoBehaviour
{
    [Tooltip("Transform to nudge. Defaults to this node's transform.")]
    [SerializeField] private Transform _shakeRoot;
    [SerializeField] private float _duration = 0.18f;
    [SerializeField] private float _amplitude = 0.09f;
    [Tooltip("Damage that produces a full-strength shake; less damage shakes proportionally less.")]
    [SerializeField] private float _damageForFullShake = 3f;

    private ResourceNode _node;
    private Coroutine _routine;
    private Vector3 _basePos;
    private int _lastHealth;

    private void Awake()
    {
        _node = GetComponent<ResourceNode>();
        if (_shakeRoot == null) _shakeRoot = transform;
        _basePos = _shakeRoot.localPosition;
    }

    private void Start() => _lastHealth = _node.CurrentHealth;

    private void OnEnable()  { _node.OnDamaged += HandleDamaged; }
    private void OnDisable()
    {
        if (_node != null) _node.OnDamaged -= HandleDamaged;
        if (_shakeRoot != null) _shakeRoot.localPosition = _basePos;
    }

    private void HandleDamaged(int remaining)
    {
        int dmg = Mathf.Max(1, _lastHealth - remaining);
        _lastHealth = remaining;
        float strength = 0.4f + 0.6f * Mathf.Clamp01(dmg / Mathf.Max(1f, _damageForFullShake));

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShakeRoutine(strength));
    }

    private IEnumerator ShakeRoutine(float strength)
    {
        Vector2 r = Random.insideUnitCircle.normalized;
        Vector3 dir = new Vector3(r.x, 0f, r.y);
        float t = 0f;
        while (t < _duration)
        {
            t += Time.deltaTime;
            float decay = 1f - (t / _duration);
            float wobble = Mathf.Sin(t * 55f) * decay * strength;
            _shakeRoot.localPosition = _basePos + dir * (_amplitude * wobble);
            yield return null;
        }
        _shakeRoot.localPosition = _basePos;
        _routine = null;
    }
}
