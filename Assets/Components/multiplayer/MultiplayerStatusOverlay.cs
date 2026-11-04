using TMPro;
using UnityEngine;

public class MultiplayerStatusOverlay : MonoBehaviour
{
    private TextMeshProUGUI statusLabel;

    [Header("Direct Panel Binding (optional)")]
    public TextMeshProUGUI player1ValueLabel;
    public TextMeshProUGUI player2ValueLabel;

    private void Start()
    {
        CreateOverlay();
        ResolvePanelLabels();
        MultiplayerMatchCoordinator.OnPublicStateChanged += RefreshPublicValues;
        RefreshPublicValues();
    }

    private void OnDestroy()
    {
        MultiplayerMatchCoordinator.OnPublicStateChanged -= RefreshPublicValues;
    }

    private void Update()
    {
        if (statusLabel == null) return;

        statusLabel.text = MultiplayerRuntime.StatusMessage;
        statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(MultiplayerRuntime.StatusMessage));

        // クライアント接続直後はイベントが来る前に表示したいので、フレームごとに同期する。
        RefreshPublicValues();
    }

    private void CreateOverlay()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject root = new GameObject("MultiplayerStatusLabel", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(900f, 60f);
        rect.anchoredPosition = new Vector2(0f, -20f);

        statusLabel = root.AddComponent<TextMeshProUGUI>();
        statusLabel.alignment = TextAlignmentOptions.Center;
        statusLabel.fontSize = 26f;
        statusLabel.color = Color.white;
        statusLabel.text = MultiplayerRuntime.StatusMessage;
    }

    private void RefreshPublicValues()
    {
        ResolvePanelLabels();
        if (player1ValueLabel == null || player2ValueLabel == null) return;

        MultiplayerMatchCoordinator coordinator = MultiplayerMatchCoordinator.Instance;
        if (coordinator == null)
        {
            player1ValueLabel.text = "P1 Money: --";
            player2ValueLabel.text = "P2 Money: --";
            return;
        }

        player1ValueLabel.text =
            $"P1 Money: {coordinator.Player0Money}";
        player2ValueLabel.text =
            $"P2 Money: {coordinator.Player1Money}";
    }

    private void ResolvePanelLabels()
    {
        if (player1ValueLabel == null)
        {
            player1ValueLabel = FindBottomPanelLabel(true);
        }

        if (player2ValueLabel == null)
        {
            player2ValueLabel = FindBottomPanelLabel(false);
        }

        if (player1ValueLabel == null || player2ValueLabel == null)
        {
            CreateFallbackBottomLabels();
        }
    }

    private TextMeshProUGUI FindBottomPanelLabel(bool leftSide)
    {
        TextMeshProUGUI[] labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Camera cam = Camera.main;

        TextMeshProUGUI best = null;
        float bestScore = float.MinValue;

        foreach (TextMeshProUGUI label in labels)
        {
            if (label == null) continue;
            if (label == statusLabel) continue;

            if (label.transform.IsChildOf(transform)) continue;

            if (label.GetComponentInParent<UnityEngine.UI.Button>() != null)
            {
                continue;
            }

            RectTransform rt = label.rectTransform;
            Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
            Vector3 viewport = cam != null
                ? cam.WorldToViewportPoint(worldCenter)
                : new Vector3(0.5f, 0.5f, 0f);

            bool isBottom = viewport.y <= 0.35f;
            bool isSide = leftSide ? viewport.x <= 0.5f : viewport.x > 0.5f;
            if (!isBottom || !isSide) continue;

            string n = label.name.ToLowerInvariant();
            int nameBonus = 0;
            if (n.Contains("money") || n.Contains("new text") || n.Contains("player") || n.Contains("p1") || n.Contains("p2"))
            {
                nameBonus += 2;
            }

            float score = nameBonus + Mathf.Abs(rt.rect.width) * 0.001f + Mathf.Abs(rt.rect.height) * 0.001f;
            if (score > bestScore)
            {
                bestScore = score;
                best = label;
            }
        }

        return best;
    }

    private void CreateFallbackBottomLabels()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        if (player1ValueLabel == null)
        {
            player1ValueLabel = CreateBottomLabel(
                canvas.transform,
                "MultiplayerPlayer1ValueLabel",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(24f, 86f),
                TextAlignmentOptions.Left,
                "P1 Money: --");
        }

        if (player2ValueLabel == null)
        {
            player2ValueLabel = CreateBottomLabel(
                canvas.transform,
                "MultiplayerPlayer2ValueLabel",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-24f, 86f),
                TextAlignmentOptions.Right,
                "P2 Money: --");
        }
    }

    private static TextMeshProUGUI CreateBottomLabel(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        TextAlignmentOptions alignment,
        string defaultText)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = new Vector2(360f, 42f);
        rect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.alignment = alignment;
        label.fontSize = 24f;
        label.color = new Color(1f, 1f, 1f, 0.98f);
        label.text = defaultText;

        return label;
    }
}
