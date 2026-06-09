using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SummonManager : MonoBehaviour
{
    [SerializeField] private int startingGold = 100;
    [SerializeField] private Canvas canvas;
    [SerializeField] private string definitionsResourcePath = "Summons";
    [SerializeField] private Vector2 worrierSpawnPosition = new Vector2(0f, -2.45f);
    [SerializeField] private Vector2[] turretSlots =
    {
        new Vector2(2.35f, -2.85f),
        new Vector2(2.35f, -1.75f),
        new Vector2(2.35f, -0.65f),
        new Vector2(2.35f, 0.45f),
        new Vector2(2.35f, 1.55f),
        new Vector2(2.35f, 2.65f)
    };
    // 召喚は右→左→右…と交互に行う
    private bool spawnRightNext = true;

    [SerializeField] private float goldTickInterval = 4f; // 秒ごとに増える間隔
    [SerializeField] private int goldPerTick = 1; // 1回あたりの増加量
    private float goldTimer = 0f;

    private readonly List<SummonDefinition> definitions = new List<SummonDefinition>();
    private int gold;
    private int nextTurretSlot;
    private Text goldText;
    private Font defaultFont;

    private void Start()
    {
        gold = startingGold;
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
        {
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        LoadDefinitions();
        BuildSummonPanel();
    }

    private void Update()
    {
        goldTimer += Time.deltaTime;
        if (goldTimer >= goldTickInterval)
        {
            gold += goldPerTick;
            goldTimer -= goldTickInterval;
            UpdateGoldText();
        }
    }

    public void Summon(SummonDefinition definition)
    {
        if (definition == null || definition.Prefab == null || gold < definition.Cost)
        {
            return;
        }

        Vector3 position;
        if (!TryGetSpawnPosition(definition.Type, out position))
        {
            return;
        }

        gold -= definition.Cost;
        UpdateGoldText();
        // 召喚位置と左右交互の決定
        // `TryGetSpawnPosition` は turret の場合 base スロットを返す（インクリメントは行わない）
        GameObject instance = null;
        if (definition.Type == SummonType.Turret)
        {
            bool spawnRight = spawnRightNext;
            Vector3 spawnPos = position;
            if (!spawnRight)
            {
                spawnPos.x = -spawnPos.x;
            }

            instance = Instantiate(definition.Prefab, spawnPos, Quaternion.identity);
            FlipSpriteHorizontally(instance, !spawnRight);

            // 右・左の両方を使ったら次のスロットへ
            if (!spawnRight)
            {
                nextTurretSlot++;
            }

            spawnRightNext = !spawnRight;
        }
        else
        {
            instance = Instantiate(definition.Prefab, position, Quaternion.identity);
            FlipSpriteHorizontally(instance, false);
        }
    }

    private void LoadDefinitions()
    {
        definitions.Clear();
        definitions.AddRange(Resources.LoadAll<SummonDefinition>(definitionsResourcePath));
        definitions.Sort((a, b) =>
        {
            int costOrder = a.Cost.CompareTo(b.Cost);
            return costOrder != 0 ? costOrder : string.CompareOrdinal(a.DisplayName, b.DisplayName);
        });
    }

    private bool TryGetSpawnPosition(SummonType summonType, out Vector3 position)
    {
        if (summonType == SummonType.Turret)
        {
            if (nextTurretSlot >= turretSlots.Length)
            {
                position = Vector3.zero;
                return false;
            }

            position = turretSlots[nextTurretSlot++];
            return true;
        }

        position = worrierSpawnPosition;
        return true;
    }

    private void FlipSpriteHorizontally(GameObject go, bool flip)
    {
        if (go == null) return;
        // SpriteRenderer に対して flipX を設定
        var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in renderers)
        {
            r.flipX = flip;
        }

        // もしスプライトが UI などで別実装なら localScale.x を反転する代替処理
        if (renderers.Length == 0)
        {
            Vector3 s = go.transform.localScale;
            s.x = Mathf.Abs(s.x) * (flip ? -1f : 1f);
            go.transform.localScale = s;
        }
    }

    private void BuildSummonPanel()
    {
        if (canvas == null)
        {
            return;
        }

        RectTransform panel = CreatePanel(canvas.transform);
        CreateText(panel, "召喚エリア", new Vector2(0f, -28f), 24, TextAnchor.MiddleCenter);

        goldText = CreateText(panel, string.Empty, new Vector2(0f, -62f), 20, TextAnchor.MiddleCenter);
        UpdateGoldText();

        for (int i = 0; i < definitions.Count; i++)
        {
            CreateSummonButton(panel, definitions[i], i);
        }
    }

    private RectTransform CreatePanel(Transform parent)
    {
        GameObject panelObject = new GameObject("SummonPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-20f, 0f);
        rect.sizeDelta = new Vector2(190f, 300f);

        Image image = panelObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.12f, 0.12f, 0.86f);

        return rect;
    }

    private void CreateSummonButton(RectTransform parent, SummonDefinition definition, int index)
    {
        GameObject buttonObject = new GameObject(definition.DisplayName + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -90f - index * 74f);
        rect.sizeDelta = new Vector2(160f, 62f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => Summon(definition));

        if (definition.Icon != null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(buttonObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(44f, 44f);
            iconObject.GetComponent<Image>().sprite = definition.Icon;
            iconObject.GetComponent<Image>().preserveAspect = true;
        }

        CreateText(rect, definition.DisplayName, new Vector2(28f, -18f), 18, TextAnchor.MiddleCenter);
        CreateText(rect, definition.Cost + "G", new Vector2(42f, -42f), 18, TextAnchor.MiddleCenter, Color.yellow);
    }

    private Text CreateText(RectTransform parent, string text, Vector2 anchoredPosition, int fontSize, TextAnchor alignment)
    {
        return CreateText(parent, text, anchoredPosition, fontSize, alignment, Color.white);
    }

    private Text CreateText(RectTransform parent, string text, Vector2 anchoredPosition, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(170f, 28f);

        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = defaultFont;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;

        return label;
    }

    private void UpdateGoldText()
    {
        if (goldText != null)
        {
            goldText.text = "Gold: " + gold;
        }
    }
}
