using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 軽量なゲーム状態管理（DontDestroyOnLoad）。
/// - StartGame(scene) でゲーム開始（LastGameScene保存）
/// - Restart() で最後に起動したゲームシーンをロード
/// - ReturnToStart(startScene) で開始画面へ戻す
/// - GameOver() で GameOverScene をロード
/// 
/// シーン名は Inspector で設定可能。
/// </summary>
public class SimpleGameManager : MonoBehaviour
{
    public static SimpleGameManager Instance { get; private set; }

    [Tooltip("ゲームオーバー表示用シーン名")]
    public string gameOverSceneName = "GameOverScene";
    
    // Awake の直後にメンバをキャッシュ
    private WaveManager waveManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        waveManager = GetComponent<WaveManager>();
    }

    // StartGame と Restart で WaveManager をリセットしてからシーンをロード
    public void StartGame(string sceneName)
    {
        PlayerPrefs.SetString("LastGameScene", sceneName);
        waveManager?.ResetState(1); // オプション: 初期Waveを1に
        EconomyManager.Instance?.ResetEconomy();
        SceneManager.LoadScene(sceneName);
    }

    public void Restart()
    {
        string last = PlayerPrefs.GetString("LastGameScene", "Battle");
        waveManager?.ResetState(1);
        EconomyManager.Instance?.ResetEconomy();
        SceneManager.LoadScene(last);
    }

    public void ReturnToStart(string startScene)
    {
        SceneManager.LoadScene(startScene);
    }

    public void GameOver()
    {
        SceneManager.LoadScene(gameOverSceneName);
    }
}