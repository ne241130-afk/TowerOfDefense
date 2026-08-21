using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

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
    private const string LastGameSceneKey = "LastGameScene";
    private const string LastGameScenePathKey = "LastGameScenePath";

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
    public void StartGame(string sceneName, string scenePath = null)
    {
        SaveLastGameScene(sceneName, scenePath);
        waveManager?.ResetState(1); // オプション: 初期Waveを1に
        EconomyManager.Instance?.ResetEconomy();
        LoadScene(sceneName, scenePath);
    }

    public void Restart()
    {
        string last = PlayerPrefs.GetString(LastGameSceneKey, "Battle");
        string lastPath = PlayerPrefs.GetString(LastGameScenePathKey, string.Empty);
        waveManager?.ResetState(1);
        EconomyManager.Instance?.ResetEconomy();
        LoadScene(last, lastPath);
    }

    public void ReturnToStart(string startScene, string startScenePath = null)
    {
        LoadScene(startScene, startScenePath);
    }

    public void GameOver()
    {
        SceneManager.LoadScene(gameOverSceneName);
    }

    public void GameClear()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameClearScene");
    }

    private void SaveLastGameScene(string sceneName, string scenePath)
    {
        PlayerPrefs.SetString(LastGameSceneKey, sceneName);
        PlayerPrefs.SetString(LastGameScenePathKey, scenePath ?? string.Empty);
    }

    private void LoadScene(string sceneName, string scenePath)
    {
#if UNITY_EDITOR
        string resolvedPath = ResolveScenePath(sceneName, scenePath);
        SceneFader_ryo.FadeToScene(sceneName, resolvedPath);
    #else
        SceneFader_ryo.FadeToScene(sceneName, scenePath);
#endif
    }

#if UNITY_EDITOR
    private static string ResolveScenePath(string sceneName, string scenePath)
    {
        if (!string.IsNullOrEmpty(scenePath))
        {
            return scenePath;
        }

        string[] sceneGuids = AssetDatabase.FindAssets($"{sceneName} t:Scene");
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                return path;
            }
        }

        return null;
    }
#endif
}