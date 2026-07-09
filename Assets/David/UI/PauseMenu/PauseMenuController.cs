using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Game pause menu. Toggles via Escape key. Pauses the game with Time.timeScale
/// and shows/hides the panel via the .hidden CSS class — same pattern as DialogueManager.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PauseMenuController : MonoBehaviour
{
    private const string OverlayName = "pause-overlay";
    private const string ResumeButtonName = "resume-button";
    private const string HiddenClass = "hidden";

    private UIDocument _document;
    private VisualElement _overlay;
    private Button _resumeButton;
    private bool _bound;
    private bool _isPaused;

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
        if (!_bound)
        {
            TryBind();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void TryBind()
    {
        if (_document == null) return;
        var root = _document.rootVisualElement;
        if (root == null) return;

        _overlay = root.Q<VisualElement>(OverlayName);
        if (_overlay == null) return;

        _resumeButton = root.Q<Button>(ResumeButtonName);
        if (_resumeButton == null) return;

        _resumeButton.RegisterCallback<ClickEvent>(_ => Resume());
        _bound = true;
    }

    private void TogglePause()
    {
        if (_isPaused) Resume();
        else Pause();
    }

    private void Pause()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        _overlay.RemoveFromClassList(HiddenClass);
    }

    private void Resume()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        _overlay.AddToClassList(HiddenClass);
    }
}