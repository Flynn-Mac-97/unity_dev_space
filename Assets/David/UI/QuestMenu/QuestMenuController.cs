using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Quest menu popup. Toggles via the T key. Shows main quests and side quests.
/// Uses the same retry-bind + .hidden CSS pattern as PauseMenuController and StatsPanelController.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class QuestMenuController : MonoBehaviour
{
    private const string OverlayName = "quest-overlay";
    private const string HiddenClass = "hidden";

    private UIDocument _document;
    private VisualElement _overlay;
    private bool _bound;
    private bool _isOpen;

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

        if (Input.GetKeyDown(KeyCode.T))
            Toggle();
    }

    private void TryBind()
    {
        if (_document == null) return;
        var root = _document.rootVisualElement;
        if (root == null) return;

        _overlay = root.Q<VisualElement>(OverlayName);
        if (_overlay == null) return;

        _bound = true;
        if (_isOpen) _overlay.RemoveFromClassList(HiddenClass);
        else _overlay.AddToClassList(HiddenClass);
    }

    private void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    public void Open()
    {
        _isOpen = true;
        if (_overlay == null) return;
        _overlay.RemoveFromClassList(HiddenClass);
    }

    public void Close()
    {
        _isOpen = false;
        if (_overlay == null) return;
        _overlay.AddToClassList(HiddenClass);
    }
}
