using UnityEngine;

/// <summary>
/// Replaces the OS mouse pointer with a custom texture via <see cref="Cursor.SetCursor"/>.
/// Cheaper than a per-frame UI-canvas reticle: set once on enable, restored on disable.
/// Assign a small read/write-enabled Texture2D (uncompressed) and the hotspot (the pixel
/// inside the texture that is the actual click point — centre for a crosshair).
/// </summary>
public class CustomCursor : MonoBehaviour
{
    [Tooltip("Cursor texture. Must be Read/Write enabled, uncompressed (RGBA32), and a Cursor/GUI texture type.")]
    [SerializeField] private Texture2D _cursor;

    [Tooltip("Click point inside the texture, in pixels from the top-left. For a centred crosshair use half the texture size.")]
    [SerializeField] private Vector2 _hotspot = Vector2.zero;

    private void OnEnable()
    {
        // Force the pointer visible — something else (or a blank texture) can leave it hidden,
        // which in the editor looks like the cursor "vanishing" on Game-view focus.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (_cursor != null)
            Cursor.SetCursor(_cursor, _hotspot, CursorMode.Auto);
    }

    private void OnDisable()
    {
        // Restore the system default pointer.
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
