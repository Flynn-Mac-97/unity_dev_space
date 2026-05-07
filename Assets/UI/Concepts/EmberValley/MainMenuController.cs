using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    private Button continueButton;
    private Button newGameButton;
    private Button settingsButton;

    private void Awake()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        continueButton = root.Q<Button>("ContinueButton");
        newGameButton = root.Q<Button>("NewGameButton");
        settingsButton = root.Q<Button>("SettingsButton");

        continueButton.clicked += () => Debug.Log("[MainMenu] Continue clicked");
        newGameButton.clicked += () => Debug.Log("[MainMenu] New Journey clicked");
        settingsButton.clicked += () => Debug.Log("[MainMenu] Settings clicked");
    }
}
