using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

/// <summary>
/// Start menu controller. Binds Start and Quit buttons. Start loads the 2D_Lighting_Demo
/// scene; Quit exits the game. Uses the same retry-bind pattern as PauseMenuController.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class StartMenuController : MonoBehaviour
{
    private const string StartButtonName = "start-button";
    private const string QuitButtonName = "quit-button";

    private UIDocument _document;
    private Button _startButton;
    private Button _quitButton;
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
        if (!_bound)
        {
            TryBind();
            return;
        }
    }

    private void TryBind()
    {
        if (_document == null) return;
        var root = _document.rootVisualElement;
        if (root == null) return;

        _startButton = root.Q<Button>(StartButtonName);
        if (_startButton == null) return;

        _quitButton = root.Q<Button>(QuitButtonName);
        if (_quitButton == null) return;

        _startButton.RegisterCallback<ClickEvent>(_ => StartGame());
        _quitButton.RegisterCallback<ClickEvent>(_ => QuitGame());
        _bound = true;
    }

    private void StartGame()
    {
        SceneManager.LoadScene("2D_Lighting_Demo");
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
