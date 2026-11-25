using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Battle scenes' lightweight time-turn control, created at runtime.</summary>
public sealed class TimeTurnButton : MonoBehaviour
{
    private Button button;
    private Image background;
    private TMP_Text label;
    private TMP_Text status;
    private TurnManager turnManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Battle" && scene.name != "MultiBattleScene") return;
        if (FindFirstObjectByType<TimeTurnButton>() != null) return;
        var root = new GameObject("TimeTurnControl");
        root.AddComponent<TimeTurnButton>();
    }

    private void Start()
    {
        turnManager = FindFirstObjectByType<TurnManager>();
        var canvasObject = new GameObject("TimeTurnCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var buttonObject = CreateRect("TimeButton", canvasObject.transform, new Vector2(1, 1), new Vector2(-105, -55), new Vector2(170, 58));
        background = buttonObject.gameObject.AddComponent<Image>();
        button = buttonObject.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(Toggle);
        label = CreateText("Time OFF", buttonObject, 24, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        var statusRect = CreateRect("TimeStatus", canvasObject.transform, new Vector2(1, 1), new Vector2(-105, -120), new Vector2(260, 48));
        status = CreateText("", statusRect, 16, TextAlignmentOptions.TopRight);
        Stretch(status.rectTransform);
        Refresh();
    }

    private void Update() => Refresh();

    private void Toggle()
    {
        if (MultiplayerRuntime.IsMultiplayerSession)
        {
            var identity = MultiplayerRuntime.LocalPlayerIdentity;
            if (identity != null) identity.CmdToggleTimeTurnReady();
            return;
        }
        if (turnManager != null) turnManager.SetTimeTurnEnabled(!turnManager.IsTimeTurnEnabled);
        Refresh();
    }

    private void Refresh()
    {
        if (button == null) return;
        bool multiplayer = MultiplayerRuntime.IsMultiplayerSession;
        var coordinator = MultiplayerMatchCoordinator.Instance;
        bool active = multiplayer? coordinator != null && coordinator.TimeTurnActive : turnManager != null && turnManager.IsTimeTurnEnabled;
        bool ready0 = coordinator != null && coordinator.TimeReady0;
        bool ready1 = coordinator != null && coordinator.TimeReady1;
        int localIndex = MultiplayerRuntime.GetEffectiveLocalPlayerIndex();
        bool localReady = localIndex == 0 ? ready0 : localIndex == 1 && ready1;
        label.text = active ? "Time ON" : localReady ? "Time READY" : "Time OFF";
        background.color = active ? new Color(0.25f, 0.8f, 0.35f) : localReady ? new Color(0.95f, 0.7f, 0.2f) : new Color(0.75f, 0.75f, 0.75f);
        if (multiplayer)
            status.text = $"Host : {(ready0 ? "READY" : "WAITING")}\nGuest : {(ready1 ? "READY" : "WAITING")}" + (active ? "\nAUTO TURN : ON" : "");
        else status.text = active ? "AUTO TURN : ON" : "";
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text CreateText(string value, RectTransform parent, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.black;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
