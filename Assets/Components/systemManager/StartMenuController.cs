using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    [Tooltip("Battle")]
    public string gameSceneName = "Battle";

    [Tooltip("Multiplayer starter component (optional). If not set, it is auto-added at runtime.")]
    public MultiplayerStartUI multiplayerStartUI;

    private void Awake()
    {
        if (multiplayerStartUI == null)
        {
            multiplayerStartUI = GetComponent<MultiplayerStartUI>();
        }

        if (multiplayerStartUI == null)
        {
            multiplayerStartUI = gameObject.AddComponent<MultiplayerStartUI>();
        }
    }

    // Startボタンにアタッチ
    public void OnStartButton()
    {
        // 次回リスタート用に保持
        PlayerPrefs.SetString("LastGameScene", gameSceneName);

        // SimpleGameManager があればそちらに任せる（ある場合は StartGame を使う）
        if (SimpleGameManager.Instance != null)
        {
            SimpleGameManager.Instance.StartGame(gameSceneName);
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    public void OnMultiplayerButton()
    {
        if (multiplayerStartUI == null)
        {
            multiplayerStartUI = GetComponent<MultiplayerStartUI>();
        }

        if (multiplayerStartUI == null)
        {
            multiplayerStartUI = gameObject.AddComponent<MultiplayerStartUI>();
        }

        multiplayerStartUI.SendMessage("OnMultiplayerButtonClicked", SendMessageOptions.DontRequireReceiver);
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