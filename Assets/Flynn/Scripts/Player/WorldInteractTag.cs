using TMPro;
using UnityEngine;

/// <summary>
/// Reusable world-space interaction tag. Drop this component on any interactable that implements
/// <see cref="IInteractionPromptProvider"/> (pickup, grapple anchor, NPC…). On first show it spawns
/// a billboarded TextMeshPro label as a child sub-object that floats above the interactable and
/// reads "[E] Pick Up Wrench", "[Q] Grapple", etc.
///
/// It does NOT detect hover itself. <see cref="WorldInteractTagPresenter"/> reads the single hover
/// source (<see cref="PlayerMouseAimer.HoveredInteractable"/>) and calls <see cref="Show"/> /
/// <see cref="Hide"/> on the hovered object's tag — so there is exactly one tag visible at a time
/// and no per-object raycast.
/// </summary>
public class WorldInteractTag : MonoBehaviour
{
    [Tooltip("Local offset of the tag above the interactable's origin (world units, Y up).")]
    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("TMP point size of the world label.")]
    [SerializeField] private float _fontSize = 4f;
    [Tooltip("Face colour of the label text.")]
    [SerializeField] private Color _textColor = Color.white;
    [Tooltip("Optional pre-authored label child. Leave empty to auto-build one at runtime.")]
    [SerializeField] private TextMeshPro _label;

    private Transform _tagRoot;

    /// <summary>True while the tag is currently displayed.</summary>
    public bool IsShown => _tagRoot != null && _tagRoot.gameObject.activeSelf;

    /// <summary>Show the tag with the given display text (e.g. the prompt's Display string).</summary>
    public void Show(string text)
    {
        EnsureBuilt();
        if (_label != null) _label.text = text;
        if (_tagRoot != null) _tagRoot.gameObject.SetActive(true);
    }

    /// <summary>Hide the tag. Safe to call when already hidden.</summary>
    public void Hide()
    {
        if (_tagRoot != null) _tagRoot.gameObject.SetActive(false);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the label sub-object once. If a label was assigned in the Inspector its existing
    /// GameObject is reused; otherwise a child "InteractTag" with a TextMeshPro + Billboard is created.
    /// </summary>
    private void EnsureBuilt()
    {
        if (_tagRoot != null) return;

        if (_label != null)
        {
            _tagRoot = _label.transform;
            return;
        }

        var go = new GameObject("InteractTag");
        _tagRoot = go.transform;
        _tagRoot.SetParent(transform, worldPositionStays: false);
        _tagRoot.localPosition = _worldOffset;

        _label = go.AddComponent<TextMeshPro>();
        _label.text = string.Empty;
        _label.fontSize = _fontSize;
        _label.color = _textColor;
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableWordWrapping = false;
        // Keep the mesh small and centred; the RectTransform drives TMP bounds.
        var rt = _label.rectTransform;
        rt.sizeDelta = new Vector2(6f, 1.5f);

        go.AddComponent<Billboard>();
        go.SetActive(false);
    }
}
