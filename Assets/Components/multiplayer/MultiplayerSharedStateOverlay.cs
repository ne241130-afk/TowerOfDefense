using TMPro;
using UnityEngine;

public class MultiplayerSharedStateOverlay : MonoBehaviour
{
    private TextMeshProUGUI label;

    private void OnEnable()
    {
        MultiplayerMatchCoordinator.OnPublicStateChanged += Refresh;
    }

    private void Start()
    {
        CreateOverlay();
        Refresh();
    }

    private void OnDisable()
    {
        MultiplayerMatchCoordinator.OnPublicStateChanged -= Refresh;
    }

    private void Update()
    {
        if (label == null) return;
        if (MultiplayerRuntime.IsMultiplayerSession)
        {
            Refresh();
        }
    }

    private void CreateOverlay()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject root = new GameObject("MultiplayerSharedStateLabel", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(540f, 120f);
        rect.anchoredPosition = new Vector2(20f, -18f);

        label = root.AddComponent<TextMeshProUGUI>();
        label.fontSize = 24f;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.color = Color.white;
        label.text = "P1 Money: - Hand: -\nP2 Money: - Hand: -";
    }

    private void Refresh()
    {
        if (label == null) return;

        MultiplayerMatchCoordinator c = MultiplayerMatchCoordinator.Instance;
        if (c == null)
        {
            label.text = "P1 Money: -  Hand: -\nP2 Money: -  Hand: -";
            return;
        }

        label.text =
            $"P1 Money: {c.Player0Money}  Hand: {c.Player0HandCount}\n" +
            $"P2 Money: {c.Player1Money}  Hand: {c.Player1HandCount}";
    }
}
