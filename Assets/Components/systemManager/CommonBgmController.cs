using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class CommonBgmController : MonoBehaviour
{
    private const string SettingsResourcePath = "Audio/CommonBgmSettings";
    private const string BgmResourcePath = "Audio/CommonBgm";
    private const string StartSceneName = "StartScene";
    private const string BattleSceneName = "Battle";
    private const string MultiBattleSceneName = "MultiBattleScene";
    private const string GameOverSceneName = "GameOverScene";

    private static CommonBgmController instance;

    [Header("Optional (Inspector)")]
    [SerializeField] private CommonBgmSettings settings;

    private AudioSource audioSource;
    private AudioClip bgmClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        EnsureInstance();
    }

    public static void StopBgm()
    {
        if (instance == null)
        {
            return;
        }

        instance.StopInternal();
    }

    public static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        CommonBgmController existing = FindFirstObjectByType<CommonBgmController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            instance.InitializeIfNeeded();
            return;
        }

        GameObject go = new GameObject("CommonBgmController");
        instance = go.AddComponent<CommonBgmController>();
        instance.InitializeIfNeeded();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        HandleSceneChanged(SceneManager.GetActiveScene().name);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleSceneChanged(scene.name);
    }

    private void InitializeIfNeeded()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (settings == null)
        {
            settings = Resources.Load<CommonBgmSettings>(SettingsResourcePath);
        }

        if (settings != null)
        {
            bgmClip = settings.bgmClip;
            audioSource.volume = Mathf.Clamp01(settings.volume);
            audioSource.loop = settings.loop;
        }
        else
        {
            audioSource.loop = true;
        }

        audioSource.playOnAwake = false;

        if (bgmClip == null)
        {
            bgmClip = Resources.Load<AudioClip>(BgmResourcePath);
            if (bgmClip == null)
            {
                Debug.LogWarning("CommonBgmController: Resources/Audio/CommonBgm is missing.");
            }
        }

        if (audioSource.clip != bgmClip)
        {
            audioSource.clip = bgmClip;
        }

        DontDestroyOnLoad(gameObject);
        HandleSceneChanged(SceneManager.GetActiveScene().name);
    }

    private void HandleSceneChanged(string sceneName)
    {
        if (IsStopScene(sceneName))
        {
            StopInternal();
            return;
        }

        if (IsPlayableScene(sceneName))
        {
            EnsurePlaying();
        }
    }

    private bool IsPlayableScene(string sceneName)
    {
        if (settings != null && settings.playableSceneNames != null && settings.playableSceneNames.Length > 0)
        {
            return ContainsSceneName(settings.playableSceneNames, sceneName);
        }

        return sceneName == StartSceneName || sceneName == BattleSceneName || sceneName == MultiBattleSceneName;
    }

    private bool IsStopScene(string sceneName)
    {
        if (settings != null && settings.stopSceneNames != null && settings.stopSceneNames.Length > 0)
        {
            return ContainsSceneName(settings.stopSceneNames, sceneName);
        }

        return sceneName == GameOverSceneName;
    }

    private static bool ContainsSceneName(string[] sceneNames, string sceneName)
    {
        for (int i = 0; i < sceneNames.Length; i++)
        {
            if (sceneNames[i] == sceneName)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsurePlaying()
    {
        if (audioSource == null || bgmClip == null)
        {
            return;
        }

        if (audioSource.clip != bgmClip)
        {
            audioSource.clip = bgmClip;
        }

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void StopInternal()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}