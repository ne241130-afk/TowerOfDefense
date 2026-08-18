using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenuController_e : MonoBehaviour
{
    [Tooltip("Battle")]
    public string gameSceneName = "Battle_ryo";
    [SerializeField] private string gameScenePath = "Assets/Scenes/example_ryo/Battle_ryo.unity";

    [SerializeField] private Button startButton;

    private void Awake()
    {
        if (startButton == null)
        {
            startButton = FindButtonByName("StartButton");
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartButton);
            startButton.onClick.AddListener(OnStartButton);
        }
        else
        {
            Debug.LogWarning($"{nameof(StartMenuController_e)} could not find StartButton in scene {gameObject.scene.name}.", this);
        }
    }

    private Button FindButtonByName(string buttonName)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    // Startボタンにアタッチ
    public void OnStartButton()
    {
        // SimpleGameManager があればそちらに任せる（ある場合は StartGame を使う）
        if (SimpleGameManager.Instance != null)
        {
            SimpleGameManager.Instance.StartGame(gameSceneName, gameScenePath);
            return;
        }

        SceneFader_ryo.FadeToScene(gameSceneName, gameScenePath);
    }

    // 終了ボタン（ビルド時のみ有効）
    public void OnQuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}