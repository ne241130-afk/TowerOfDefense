using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class SceneFader_ryo : MonoBehaviour
{
    private const string FaderObjectName = "__SceneFader_ryo";

    private static SceneFader_ryo instance;

    [SerializeField] private float fadeDuration = 0.35f;

    private CanvasGroup canvasGroup;
    private Image fadeImage;
    private bool isTransitioning;

    public static void FadeToScene(string sceneName, string scenePath = null)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"{nameof(SceneFader_ryo)}: sceneName is empty.");
            return;
        }

        SceneFader_ryo fader = EnsureInstance();
        if (fader == null || fader.isTransitioning)
        {
            return;
        }

        fader.StartCoroutine(fader.FadeAndLoadScene(sceneName, scenePath));
    }

    private static SceneFader_ryo EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject root = new GameObject(FaderObjectName);
        instance = root.AddComponent<SceneFader_ryo>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        SetOverlayAlpha(0f, false);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // 画面全体を覆う黒いUIをランタイムで構築する
    private void BuildOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        GameObject imageObject = new GameObject("FadeImage");
        imageObject.transform.SetParent(transform, false);

        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeImage = imageObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
    }

    private IEnumerator FadeAndLoadScene(string sceneName, string scenePath)
    {
        isTransitioning = true;

        yield return Fade(0f, 1f);
        yield return LoadScene(sceneName, scenePath);
        yield return null;
        yield return Fade(1f, 0f);

        isTransitioning = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        SetOverlayAlpha(from, true);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(from, to, t);
            SetOverlayAlpha(alpha, true);
            yield return null;
        }

        bool blockInput = to > 0f;
        SetOverlayAlpha(to, blockInput);
    }

    private IEnumerator LoadScene(string sceneName, string scenePath)
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(scenePath) && SceneUtility.GetBuildIndexByScenePath(scenePath) == -1)
        {
            EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield break;
        }
#endif

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (operation != null && !operation.isDone)
        {
            yield return null;
        }
    }

    private void SetOverlayAlpha(float alpha, bool blockInput)
    {
        if (canvasGroup == null || fadeImage == null)
        {
            return;
        }

        canvasGroup.alpha = alpha;
        canvasGroup.interactable = blockInput;
        canvasGroup.blocksRaycasts = blockInput;
        fadeImage.raycastTarget = blockInput;
    }
}