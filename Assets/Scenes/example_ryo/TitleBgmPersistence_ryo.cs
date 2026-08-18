using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class TitleBgmPersistence_ryo : MonoBehaviour
{
    [Header("このシーン間ではBGMを維持する")]
    [SerializeField] private string[] persistentSceneNames = { "Title_ryo", "HowToPlay_ryo" };

    [Tooltip("一覧にないシーンへ移動したら、このBGMオブジェクトを破棄する")]
    [SerializeField] private bool keepPlayingOnlyInListedScenes = true;

    private static TitleBgmPersistence_ryo instance;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // 2つ目以降のBGMオブジェクトは破棄して、再生を途切れさせない
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void Start()
    {
        // 起動直後のシーンも判定対象にする
        EvaluateCurrentScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EvaluateCurrentScene(scene.name);
    }

    private void EvaluateCurrentScene(string sceneName)
    {
        if (!keepPlayingOnlyInListedScenes)
        {
            return;
        }

        if (IsPersistentScene(sceneName))
        {
            return;
        }

        Destroy(gameObject);
    }

    private bool IsPersistentScene(string sceneName)
    {
        if (persistentSceneNames == null || persistentSceneNames.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < persistentSceneNames.Length; i++)
        {
            if (persistentSceneNames[i] == sceneName)
            {
                return true;
            }
        }

        return false;
    }
}