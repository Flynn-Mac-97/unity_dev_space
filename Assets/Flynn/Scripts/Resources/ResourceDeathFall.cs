using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the break animation when a <see cref="ResourceNode"/> is depleted: disables its colliders,
/// disables the <see cref="Billboard"/> on the visual (so our topple rotation sticks), then topples
/// the sprite while fading its alpha to zero, and finally destroys the GameObject. Owns the node's
/// removal — its presence is what tells <see cref="ResourceNode"/> not to self-destroy on depletion.
/// </summary>
[RequireComponent(typeof(ResourceNode))]
public class ResourceDeathFall : MonoBehaviour
{
    [Tooltip("Visual to topple/fade. Defaults to this transform.")]
    [SerializeField] private Transform _visualRoot;
    [Tooltip("Sprite faded to transparent during the fall. Auto-found in children if empty.")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private float _fallDuration = 0.7f;
    [SerializeField] private float _fadeDuration = 0.55f;
    [Tooltip("Degrees the sprite topples as it falls.")]
    [SerializeField] private float _toppleAngle = 82f;
    [Tooltip("World units the sprite sinks as it falls.")]
    [SerializeField] private float _sink = 0.35f;

    private ResourceNode _node;

    private void Awake()
    {
        _node = GetComponent<ResourceNode>();
        if (_visualRoot == null) _visualRoot = transform;
        if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()  { _node.OnDepleted += HandleDepleted; }
    private void OnDisable() { if (_node != null) _node.OnDepleted -= HandleDepleted; }

    private void HandleDepleted()
    {
        _node.OnDepleted -= HandleDepleted;

        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        // Stop the billboard from overwriting the topple rotation each LateUpdate.
        Billboard billboard = GetComponentInChildren<Billboard>();
        if (billboard != null) billboard.enabled = false;

        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        Vector3 startPos = _visualRoot.position;
        Quaternion startRot = _visualRoot.rotation;
        Vector3 axis = Random.value < 0.5f ? Vector3.forward : Vector3.back;

        Color baseColor = _spriteRenderer != null ? _spriteRenderer.color : Color.white;
        float total = Mathf.Max(_fallDuration, _fadeDuration);
        float t = 0f;

        while (t < total)
        {
            t += Time.deltaTime;
            float fall = Mathf.Clamp01(t / _fallDuration);
            float eased = fall * fall; // accelerate as it falls
            _visualRoot.rotation = startRot * Quaternion.AngleAxis(_toppleAngle * eased, axis);
            _visualRoot.position = startPos + Vector3.down * (_sink * eased);

            if (_spriteRenderer != null)
            {
                float alpha = baseColor.a * (1f - Mathf.Clamp01(t / _fadeDuration));
                _spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
