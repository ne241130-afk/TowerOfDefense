using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// カード配置全体を統括するコントローラー。
///   1. カードが選択されると、マウスカーソル位置に効果範囲をハイライト表示する
///   2. フィールドをクリックすると、その場所に実際の効果(IFieldEffect)を設置する
///   3. 設置と同時に見た目のプレハブを配置し、カードを手札から消費する
///
/// シーンに1つ配置してください。
/// ※このスクリプトは旧Input(UnityEngine.Input)を使用しています。
///   プロジェクトが新Input Systemのみの設定になっている場合は、
///   Project Settings > Player > Active Input Handling を「Both」にするか、
///   マウス取得部分を新Input Systemに置き換えてください。
/// </summary>
public class CardPlacementController : MonoBehaviour
{
    public static CardPlacementController Instance { get; private set; }

    [Header("参照")]
    public Camera targetCamera;

    [Tooltip("配置プレビュー用のハイライト表示プレハブ(半透明の四角スプライトなど)")]
    public GameObject highlightPrefab;

    [Tooltip("カード側にplacedVisualPrefabが未設定の場合に使うデフォルトの見た目")]
    public GameObject defaultPlacedEffectVisualPrefab;

    private CardData selectedCard;
    private CardSlotUI selectedSlot;
    private readonly List<GameObject> highlightInstances = new List<GameObject>();
    private Vector3Int? lastHoverCell;

    /// <summary>highlightPrefab未設定時に使うフォールバック用スプライト(起動時に1x1テクスチャから生成)。</summary>
    private Sprite fallbackHighlightSprite;

    public bool HasSelection => selectedCard != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // highlightPrefabが未設定の場合に備えて1x1白スプライトを生成しておく
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        fallbackHighlightSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    /// <summary>
    /// CardSlotUIのクリックから呼ばれる。同じカードをもう一度選ぶと選択解除になる。
    /// </summary>
    public void SelectCard(CardData card, CardSlotUI slot)
    {
        if (selectedCard == card && selectedSlot == slot)
        {
            ClearSelection();
            return;
        }

        selectedSlot?.SetSelectedVisual(false);

        selectedCard = card;
        selectedSlot = slot;
        selectedSlot.SetSelectedVisual(true);

        lastHoverCell = null; // 次のUpdateで必ずハイライトを再計算させる
    }

    public void ClearSelection()
    {
        selectedSlot?.SetSelectedVisual(false);
        selectedCard = null;
        selectedSlot = null;
        lastHoverCell = null;
        HideHighlights();
    }

    private void Update()
    {
        if (selectedCard == null) return;
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null) return;

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        Vector3 mouseWorld = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector3Int hoverCell = FieldGridConfig.Instance.grid.WorldToCell(mouseWorld);

        if (lastHoverCell == null || hoverCell != lastHoverCell.Value)
        {
            UpdateHighlights(hoverCell);
            lastHoverCell = hoverCell;
        }

        // カードUIなどのUI要素上のクリックは配置確定として扱わない
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            TryPlaceAt(hoverCell);
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void UpdateHighlights(Vector3Int center)
    {
        HideHighlights();

        if (selectedCard == null) return;

        // ハンター召喚カードは範囲設定を他のカードと同様にareaRadiusで表示
        IReadOnlyList<Vector3Int> cells = EffectAreaUtility.GetSquareArea(center, selectedCard.areaRadius);

        foreach (var cell in cells)
        {
            if (!FieldGridConfig.Instance.IsWalkable(cell)) continue;

            Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
            GameObject go = highlightPrefab != null
                ? Instantiate(highlightPrefab, worldPos, Quaternion.identity)
                : CreateFallbackHighlight(worldPos);
            highlightInstances.Add(go);
        }
    }

    /// <summary>
    /// highlightPrefabが未設定の場合のフォールバックハイライト。
    /// SpriteRendererで半透明オレンジのセルサイズ矩形を生成する。
    /// </summary>
    private GameObject CreateFallbackHighlight(Vector3 worldPos)
    {
        var go = new GameObject("HighlightFallback");
        go.transform.position = worldPos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = fallbackHighlightSprite;
        sr.color = new Color(1f, 0.7f, 0f, 0.4f);
        sr.sortingOrder = 10;

        Vector3 cellSize = FieldGridConfig.Instance.grid.cellSize;
        go.transform.localScale = new Vector3(cellSize.x, cellSize.y, 1f);

        return go;
    }

    private void HideHighlights()
    {
        foreach (var go in highlightInstances)
        {
            if (go != null) Destroy(go);
        }
        highlightInstances.Clear();
    }

    private void TryPlaceAt(Vector3Int center)
    {
        // 招き猫はフィールド設置不要。どこかがクリックされた時点で即時発動する
        if (selectedCard.effectType == CardEffectType.ManekinNeko)
        {
            if (EconomyManager.Instance != null && selectedCard.cost > 0)
            {
                if (!EconomyManager.Instance.TrySpend(selectedCard.cost))
                {
                    Debug.Log($"{selectedCard.cardName}: 所持金不足（必要: {selectedCard.cost}、所持: {EconomyManager.Instance.CurrentMoney}）");
                    return;
                }
            }
            UseManekinNeko();
            return;
        }

        if (!FieldGridConfig.Instance.IsWalkable(center)) return;

        // コスト確認・消費(所持金不足なら配置キャンセル)
        if (EconomyManager.Instance != null && selectedCard.cost > 0)
        {
            if (!EconomyManager.Instance.TrySpend(selectedCard.cost))
            {
                Debug.Log($"{selectedCard.cardName}: 所持金不足（必要: {selectedCard.cost}、所持: {EconomyManager.Instance.CurrentMoney}）");
                return;
            }
        }

        // ハンター召喚カードはIFieldEffectを使わず、プレハブを直接1体配置する
        if (selectedCard.effectType == CardEffectType.SummonHunter)
        {
            PlaceHunter(center);
            return;
        }

        // 肉カードもビジュアルを1つだけ中心セルに置く
        if (selectedCard.effectType == CardEffectType.Meat)
        {
            PlaceMeat(center);
            return;
        }
        
        if (selectedCard.effectType == CardEffectType.Fluit)
        {
            PlaceFluit(center);
            return;
        }

        // 捕獲ネットランチャーは範囲内の動物を即時捕獲する
        if (selectedCard.effectType == CardEffectType.NetLauncher)
        {
            PlaceNetLauncher(center);
            return;
        }

        var cells = EffectAreaUtility.GetSquareArea(center, selectedCard.areaRadius);

        foreach (var cell in cells)
        {
            if (!FieldGridConfig.Instance.IsWalkable(cell)) continue;

            IFieldEffect effect = CardEffectFactory.CreateEffect(selectedCard.effectType);
            if (effect == null) continue;

            FieldEffectMap.Instance.SetEffect(cell, effect);

            GameObject visualPrefab = selectedCard.placedVisualPrefab != null
                ? selectedCard.placedVisualPrefab
                : defaultPlacedEffectVisualPrefab;

            if (visualPrefab != null)
            {
                Instantiate(
                    visualPrefab,
                    FieldGridConfig.Instance.grid.GetCellCenterWorld(cell),
                    Quaternion.identity
                );
            }
        }
        
        // カードを使用
        selectedSlot.ConsumeCard();

        // カード選択を解除
        ClearSelection();

        // カードを使用したので1ターン進める
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.AdvanceTurn();
        }
    }

    /// <summary>
    /// ハンター召喚カードの配置処理。指定セルにhunterPrefabを1体インスタンス化する。
    /// placedVisualPrefab が設定されていればそのセルに背景スプライトを表示する。
    /// </summary>
    private void PlaceHunter(Vector3Int cell)
    {
        if (selectedCard.hunterPrefab == null)
        {
            Debug.LogWarning($"{selectedCard.cardName}: hunterPrefabが設定されていません。", this);
            return;
        }

        Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        var instance = Instantiate(selectedCard.hunterPrefab, worldPos, Quaternion.identity);

        // カードのareaRadiusをハンターのactionRadiusとして設定
        var hunter = instance.GetComponent<HunterController>();
        if (hunter != null)
        {
            hunter.data.actionRadius = selectedCard.areaRadius;
        }

        // placedVisualPrefab（未設定時はdefaultPlacedEffectVisualPrefab）で配置先のセルに背景を表示
        GameObject visualPrefab = selectedCard.placedVisualPrefab != null
            ? selectedCard.placedVisualPrefab
            : defaultPlacedEffectVisualPrefab;

        if (visualPrefab != null)
        {
            Instantiate(visualPrefab, worldPos, Quaternion.identity);
        }

        selectedSlot.ConsumeCard();
        ClearSelection();

        // カードを使用したので1ターン進める
        TurnManager.Instance?.AdvanceTurn();
    }

    /// <summary>
    /// 肉カードの配置処理。中心セルにフィールドエフェクトを1つ登録し、
    /// ビジュアルプレハブを1体だけ生成する。大きさはプレハブ側のスケールで調整できる。
    /// </summary>
    private void PlaceMeat(Vector3Int cell)
    {
        if (selectedCard.placedVisualPrefab == null && defaultPlacedEffectVisualPrefab == null)
        {
            Debug.LogWarning($"{selectedCard.cardName}: placedVisualPrefabが設定されていません。", this);
            return;
        }

        // フィールドエフェクトを中心の1セルに登録
        IFieldEffect effect = CardEffectFactory.CreateEffect(selectedCard.effectType);
        if (effect != null)
        {
            // 誘引範囲をCardDataのareaRadiusから設定
            if (effect is MeatFieldEffect meatEffect)
                meatEffect.AttractionRange = selectedCard.areaRadius;
            FieldEffectMap.Instance.SetEffect(cell, effect);
        }

        // ビジュアルを1つだけ中心ワールド座標に生成
        Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        GameObject visualPrefab = selectedCard.placedVisualPrefab != null
            ? selectedCard.placedVisualPrefab
            : defaultPlacedEffectVisualPrefab;
        Instantiate(visualPrefab, worldPos, Quaternion.identity);

        selectedSlot.ConsumeCard();
        ClearSelection();
        TurnManager.Instance?.AdvanceTurn();
    }

    private void PlaceFluit(Vector3Int cell)
    {
        if (selectedCard.placedVisualPrefab == null && defaultPlacedEffectVisualPrefab == null)
        {
            Debug.LogWarning($"{selectedCard.cardName}: placedVisualPrefabが設定されていません。", this);
            return;
        }

        // フィールドエフェクトを中心の1セルに登録
        IFieldEffect effect = CardEffectFactory.CreateEffect(selectedCard.effectType);
        if (effect != null)
        {
            // 誘引範囲をCardDataのareaRadiusから設定
            if (effect is FluitFieldEffect fluitEffect)
                fluitEffect.AttractionRange = selectedCard.areaRadius;
            FieldEffectMap.Instance.SetEffect(cell, effect);
        }

        // ビジュアルを1つだけ中心ワールド座標に生成
        Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        GameObject visualPrefab = selectedCard.placedVisualPrefab != null
            ? selectedCard.placedVisualPrefab
            : defaultPlacedEffectVisualPrefab;
        Instantiate(visualPrefab, worldPos, Quaternion.identity);

        selectedSlot.ConsumeCard();
        ClearSelection();
        TurnManager.Instance?.AdvanceTurn();
    }

    /// <summary>
    /// 捕獲ネットランチャーの処理。
    /// areaRadius の範囲内にいるすべての動物を即時捕獲し、
    /// placedVisualPrefab があればその位置に生成する(エフェクト用)。
    /// 永続フィールドエフェクトは登録しない。
    /// </summary>
    private void PlaceNetLauncher(Vector3Int center)
    {
        var cells = EffectAreaUtility.GetSquareArea(center, selectedCard.areaRadius);

        int captured = 0;
        foreach (var cell in cells)
        {
            if (AnimalOccupancyMap.Instance == null) break;
            if (!AnimalOccupancyMap.Instance.TryGetAnimalAt(cell, out var animal)) continue;
            if (animal == null) continue;

            Debug.Log($"[NetLauncher] {animal.Stats.animalName} を捕獲した！");
            CaptureCounter.Instance?.AddCapture();
            WaveManager.Instance?.AddCapture();
            Destroy(animal.gameObject);
            captured++;
        }

        Debug.Log($"[NetLauncher] {captured} 体を捕獲。");

        // ビジュアルエフェクトを範囲中心に生成(設定されていれば)
        GameObject visualPrefab = selectedCard.placedVisualPrefab != null
            ? selectedCard.placedVisualPrefab
            : defaultPlacedEffectVisualPrefab;
        if (visualPrefab != null)
        {
            Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(center);
            Instantiate(visualPrefab, worldPos, Quaternion.identity);
        }

        selectedSlot.ConsumeCard();
        ClearSelection();
        TurnManager.Instance?.AdvanceTurn();
    }

    /// <summary>
    /// 招き猫カードの使用処理。
    /// bonusAmount 分だけ即時所持金を増やし、カードを消費してターンを進める。
    /// フィールドには何も置かない。
    /// </summary>
    private void UseManekinNeko()
    {
        if (selectedCard.bonusAmount > 0 && EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddMoney(selectedCard.bonusAmount);
            Debug.Log($"[招き猫] {selectedCard.bonusAmount} 円獲得！ 現在の所持金: {EconomyManager.Instance.CurrentMoney}");
        }

        selectedSlot.ConsumeCard();
        ClearSelection();
        TurnManager.Instance?.AdvanceTurn();
    }
}
