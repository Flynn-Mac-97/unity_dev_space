using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// David's Player HUD controller. Displays a progress bar in the top-left corner.
/// Uses the retry-bind pattern: the visual tree may not be ready during OnEnable,
/// so binding is retried from Update until it succeeds.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PlayerHudController : MonoBehaviour
{
    private const string ProgressFillName = "ProgressFill";
    private const string ProgressLabelName = "ProgressLabel";

    [Tooltip("Progress value 0–1. Can be set at runtime via SetProgress().")]
    [SerializeField, Range(0f, 1f)] private float _progress = 1f;

    private UIDocument _document;
    private VisualElement _progressFill;
    private Label _progressLabel;
    private bool _bound;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        _bound = false;
        TryBind();
    }

    private void Update()
    {
        if (!_bound) TryBind();
    }

    private void TryBind()
    {
        if (_document == null) return;
        var root = _document.rootVisualElement;
        if (root == null) return;

        _progressFill = root.Q<VisualElement>(ProgressFillName);
        if (_progressFill == null) return; // tree not ready yet

        _progressLabel = root.Q<Label>(ProgressLabelName);
        _bound = true;
        UpdateProgress(_progress);
    }

    /// <summary>Set the progress bar value. <paramref name="value"/> is clamped to 0–1.</summary>
    public void SetProgress(float value)
    {
        _progress = Mathf.Clamp01(value);
        if (_bound) UpdateProgress(_progress);
    }

    private void UpdateProgress(float value)
    {
        _progressFill.style.width = new StyleLength(Length.Percent(value * 100f));
        if (_progressLabel != null)
            _progressLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}