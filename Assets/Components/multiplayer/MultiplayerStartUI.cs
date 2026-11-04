using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

public class MultiplayerStartUI : MonoBehaviour
{
    [Header("任意参照")]
    public Button multiplayerButton;
    public Canvas targetCanvas;

    private GameObject panelRoot;
    private TMP_InputField addressInput;
    private TextMeshProUGUI statusText;
    private Coroutine autoConnectRoutine;
    private CancellationTokenSource autoConnectCts;

    private void Awake()
    {
        EnsureNetworkManagerExists();
        EnsureMultiplayerButton();
    }

    private void Update()
    {
        if (statusText != null)
        {
            statusText.text = MultiplayerRuntime.StatusMessage;
        }
    }

    private void EnsureMultiplayerButton()
    {
        if (multiplayerButton == null)
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Button b in buttons)
            {
                if (b == null) continue;
                if (b.name == "MultiButton")
                {
                    multiplayerButton = b;
                    break;
                }
            }
        }

        if (multiplayerButton == null)
        {
            Button startButton = FindButtonByName("StartButton");
            if (startButton != null)
            {
                GameObject clone = Instantiate(startButton.gameObject, startButton.transform.parent);
                clone.name = "MultiButton";

                RectTransform rect = clone.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition += new Vector2(0f, -90f);
                }

                TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = "MULTI PLAY";
                }

                multiplayerButton = clone.GetComponent<Button>();
            }
        }

        if (multiplayerButton != null)
        {
            // StartButton複製時に持ち越される既存OnClick(Persistent含む)を無効化
            // して、マルチプレイ用のクリックだけを登録する。
            multiplayerButton.onClick = new Button.ButtonClickedEvent();
            multiplayerButton.onClick.AddListener(OnMultiplayerButtonClicked);
        }
    }

    private static Button FindButtonByName(string name)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button.name == name) return button;
        }

        return null;
    }

    private void EnsureNetworkManagerExists()
    {
        if (NetworkManager.singleton != null)
        {
            if (NetworkManager.singleton.transport == null)
            {
                Transport existingTransport = NetworkManager.singleton.GetComponent<Transport>();
                if (existingTransport == null)
                {
                    existingTransport = NetworkManager.singleton.gameObject.AddComponent<TelepathyTransport>();
                }
                NetworkManager.singleton.transport = existingTransport;
            }

            if (NetworkManager.singleton is MultiplayerNetworkManager existingManager)
            {
                existingManager.startSceneName = "StartScene";
                existingManager.multiBattleSceneName = "MultiBattleScene";
                existingManager.networkAddress = "127.0.0.1";
            }

            return;
        }

        GameObject go = new GameObject("MultiplayerNetworkManager");
        TelepathyTransport transport = go.AddComponent<TelepathyTransport>();
        MultiplayerNetworkManager manager = go.AddComponent<MultiplayerNetworkManager>();

        manager.transport = transport;
        manager.networkAddress = "127.0.0.1";
        manager.startSceneName = "StartScene";
        manager.multiBattleSceneName = "MultiBattleScene";
    }

    private void OnMultiplayerButtonClicked()
    {
        if (panelRoot == null)
        {
            BuildPanel();
        }

        panelRoot.SetActive(true);
        MultiplayerRuntime.SetStatus("接続方法を選択してください");
    }

    private void BuildPanel()
    {
        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
        }

        if (targetCanvas == null)
        {
            Debug.LogError("MultiplayerStartUI: Canvas が見つかりません。");
            return;
        }

        panelRoot = new GameObject("MultiplayerPanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(targetCanvas.transform, false);

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(540f, 360f);

        Image bg = panelRoot.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.8f);

        statusText = CreateLabel(panelRoot.transform, "StatusText", "未接続", new Vector2(0f, 120f), 24);

        addressInput = CreateInput(panelRoot.transform, "AddressInput", "127.0.0.1", new Vector2(0f, 40f));

        Button hostButton = CreateButton(panelRoot.transform, "HostButton", "HOST", new Vector2(-140f, -40f));
        hostButton.onClick.AddListener(() =>
        {
            CancelAutoConnect();
            MultiplayerRuntime.SetStatus("ホストを開始しています...");
            if (NetworkManager.singleton != null)
            {
                NetworkManager.singleton.StartHost();
            }
        });

        Button clientButton = CreateButton(panelRoot.transform, "ClientButton", "CLIENT", new Vector2(140f, -40f));
        clientButton.onClick.AddListener(() =>
        {
            CancelAutoConnect();
            if (NetworkManager.singleton != null)
            {
                NetworkManager.singleton.networkAddress = addressInput != null ? addressInput.text : "127.0.0.1";
                MultiplayerRuntime.SetStatus("クライアント接続中...");
                NetworkManager.singleton.StartClient();
            }
        });

        Button autoButton = CreateButton(panelRoot.transform, "AutoButton", "AUTO", new Vector2(0f, -40f));
        autoButton.onClick.AddListener(OnAutoConnectClicked);

        Button closeButton = CreateButton(panelRoot.transform, "CloseButton", "CLOSE", new Vector2(0f, -130f));
        closeButton.onClick.AddListener(() =>
        {
            CancelAutoConnect();
            panelRoot.SetActive(false);
        });
    }

    private void OnAutoConnectClicked()
    {
        if (autoConnectRoutine != null) return;
        autoConnectRoutine = StartCoroutine(AutoConnectRoutine());
    }

    private IEnumerator AutoConnectRoutine()
    {
        EnsureNetworkManagerExists();

        if (!(NetworkManager.singleton is MultiplayerNetworkManager manager))
        {
            MultiplayerRuntime.SetStatus("NetworkManagerの初期化に失敗しました");
            autoConnectRoutine = null;
            yield break;
        }

        MultiplayerLanDiscovery discovery = manager.lanDiscovery;
        if (discovery == null)
        {
            discovery = manager.GetComponent<MultiplayerLanDiscovery>();
            if (discovery == null)
            {
                discovery = manager.gameObject.AddComponent<MultiplayerLanDiscovery>();
            }
            manager.lanDiscovery = discovery;
        }

        CancelAutoConnect();
        autoConnectCts = new CancellationTokenSource();

        MultiplayerRuntime.SetStatus("LAN内のホストを検索中...");

        Task<string> discoverTask = discovery.DiscoverHostAsync(2.5f, autoConnectCts.Token);
        yield return new WaitUntil(() => discoverTask.IsCompleted);

        string foundAddress = null;
        if (!discoverTask.IsFaulted && !discoverTask.IsCanceled)
        {
            foundAddress = discoverTask.Result;
        }

        if (!string.IsNullOrEmpty(foundAddress))
        {
            if (addressInput != null)
            {
                addressInput.text = foundAddress;
            }

            manager.networkAddress = foundAddress;
            MultiplayerRuntime.SetStatus("ホスト発見: " + foundAddress + " に接続中...");
            manager.StartClient();
        }
        else
        {
            float backoff = Random.Range(0.25f, 0.8f);
            MultiplayerRuntime.SetStatus("ホスト未検出。再確認中...");
            yield return new WaitForSeconds(backoff);

            Task<string> retryTask = discovery.DiscoverHostAsync(1.2f, CancellationToken.None);
            yield return new WaitUntil(() => retryTask.IsCompleted);
            string retryAddress = (!retryTask.IsFaulted && !retryTask.IsCanceled) ? retryTask.Result : null;

            if (!string.IsNullOrEmpty(retryAddress))
            {
                if (addressInput != null)
                {
                    addressInput.text = retryAddress;
                }

                manager.networkAddress = retryAddress;
                MultiplayerRuntime.SetStatus("ホスト発見: " + retryAddress + " に接続中...");
                manager.StartClient();
            }
            else
            {
                MultiplayerRuntime.SetStatus("ホスト未検出。自端末をホストとして起動します...");
                manager.StartHost();
            }
        }

        autoConnectRoutine = null;
    }

    private void CancelAutoConnect()
    {
        if (autoConnectCts == null) return;

        try
        {
            autoConnectCts.Cancel();
        }
        catch
        {
        }

        autoConnectCts.Dispose();
        autoConnectCts = null;
        autoConnectRoutine = null;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string objectName, string text, Vector2 anchoredPos, int fontSize)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(480f, 80f);
        rect.anchoredPosition = anchoredPos;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        return label;
    }

    private static TMP_InputField CreateInput(Transform parent, string objectName, string defaultText, Vector2 anchoredPos)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(360f, 56f);
        rect.anchoredPosition = anchoredPos;

        Image img = root.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.15f);

        TMP_InputField input = root.AddComponent<TMP_InputField>();

        TextMeshProUGUI textComponent = CreateLabel(root.transform, "Text", defaultText, Vector2.zero, 28);
        textComponent.alignment = TextAlignmentOptions.Left;
        textComponent.margin = new Vector4(16f, 0f, 0f, 0f);

        TextMeshProUGUI placeholder = CreateLabel(root.transform, "Placeholder", "接続先IP", Vector2.zero, 24);
        placeholder.color = new Color(1f, 1f, 1f, 0.5f);
        placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.margin = new Vector4(16f, 0f, 0f, 0f);

        input.textViewport = rect;
        input.textComponent = textComponent;
        input.placeholder = placeholder;
        input.text = defaultText;

        return input;
    }

    private static Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(220f, 64f);
        rect.anchoredPosition = anchoredPos;

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.2f, 0.45f, 0.75f, 0.95f);

        CreateLabel(go.transform, "Label", label, Vector2.zero, 30);

        return go.GetComponent<Button>();
    }
}
