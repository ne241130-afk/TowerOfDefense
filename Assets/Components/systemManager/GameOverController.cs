using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    [Tooltip("開始画面のシーン名（StartSceneを作る場合など）")]
    public string startSceneName = "StartScene";

    // リスタート（最後にプレイしたゲームシーンをロード）
    public void OnRestartButton()
    {
        // SimpleGameManager があればそちらを使う
        if (SimpleGameManager.Instance != null)
        {
            SimpleGameManager.Instance.Restart();
            return;
        }

        string last = PlayerPrefs.GetString("LastGameScene", "Battle");
        SceneManager.LoadScene(last);
    }

    // 開始画面に戻る
    public void OnReturnToStartButton()
    {
        if (SimpleGameManager.Instance != null)
        {
            SimpleGameManager.Instance.ReturnToStart(startSceneName);
            return;
        }

        SceneManager.LoadScene(startSceneName);
    }
}