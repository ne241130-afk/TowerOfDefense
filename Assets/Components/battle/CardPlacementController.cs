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

    public bool HasSelection => selectedCard != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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

        if (highlightPrefab == null || selectedCard == null) return;

        var cells = EffectAreaUtility.GetSquareArea(center, selectedCard.areaRadius);
        foreach (var cell in cells)
        {
            if (!FieldGridConfig.Instance.IsWalkable(cell)) continue;

            var go = Instantiate(
                highlightPrefab,
                FieldGridConfig.Instance.grid.GetCellCenterWorld(cell),
                Quaternion.identity);
            highlightInstances.Add(go);
        }
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
        if (!FieldGridConfig.Instance.IsWalkable(center)) return;

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
                Instantiate(visualPrefab, FieldGridConfig.Instance.grid.GetCellCenterWorld(cell), Quaternion.identity);
            }
        }

        selectedSlot.ConsumeCard();
        ClearSelection();
    }
}
