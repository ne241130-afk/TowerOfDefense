using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    [Tooltip("Battle")]
    public string gameSceneName = "Battle";

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