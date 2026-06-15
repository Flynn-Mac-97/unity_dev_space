using UnityEngine;

/// <summary>
/// A crate the player can push via normal physics collision.
/// Has <see cref="WindResistant"/> so wind zones do not push it,
/// but it blocks wind from reaching objects behind it.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(WindResistant))]
public class PushableCrate2D : MonoBehaviour
{
    [Tooltip("Drag applied so the crate stops sliding when not pushed.")]
    [SerializeField] private float _drag = 5f;

    private void Reset()
    {
        var rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.drag = _drag;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Awake()
    {
        var rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.drag = _drag;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }
}
