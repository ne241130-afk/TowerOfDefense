using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 独立アクション「威嚇」の入力を管理する。
/// - ボタン押下で威嚇モードへ切り替え
/// - 動物のいるセルをクリックしたときのみ後退を実行
/// - 成功時のみコスト消費 + 1ターン進行
/// </summary>
public class IntimidateActionController : MonoBehaviour
{
    public static IntimidateActionController Instance { get; private set; }

    [Header("参照")]
    public Button intimidateButton;
    public Camera targetCamera;

    [Header("威嚇設定")]
    [Tooltip("威嚇成功時に消費するコスト。既存カードの標準コスト(50)に合わせる。")]
    public int intimidateCost = 50;

    [Header("モード表示")]
    [Tooltip("威嚇モード中のみ有効化するUI。未設定でも動作可能。")]
    public GameObject aimingModeIndicator;

    [Tooltip("ボタンの色変化に使うImage。未設定ならintimidateButtonから自動取得。")]
    public Image buttonImage;

    public Color normalButtonColor = Color.white;
    public Color aimingButtonColor = new Color(1f, 0.8f, 0.45f, 1f);

    [Header("後退先プレビュー")]
    [Tooltip("威嚇モード中、後退先セルに表示する塗りつぶし色。赤の次ターンハイライトと同じ視覚スタイルに合わせる")]
    public Color retreatPreviewColor = new Color(0.10f, 0.45f, 1f, 0.28f);
    [Tooltip("枠線の濃い青色。赤の枠線と同じ見え方にするため、濃く・少し透けた感じにする")]
    public Color retreatPreviewBorderColor = new Color(0.08f, 0.20f, 1f, 0.95f);
    [Tooltip("枠線の太さ。赤の nextCellBorderWidth=0.5 を基準にする")]
    [Range(0.02f, 1f)]
    public float retreatPreviewBorderThickness = 0.5f;

    public bool IsAiming => isAiming;

    private bool isAiming;
    private bool runtimeClickListenerAdded;
    private GameObject retreatPreviewObject;
    private SpriteRenderer retreatPreviewRenderer;
    private GameObject retreatPreviewBorderObject;
    private LineRenderer retreatPreviewBorderRenderer;
    private Texture2D retreatPreviewTexture;
    private Sprite retreatPreviewSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (intimidateButton == null)
        {
            intimidateButton = GetComponent<Button>();
        }

        if (buttonImage == null && intimidateButton != null)
        {
            buttonImage = intimidateButton.targetGraphic as Image;
        }

        // 以前の青ハイライト残骸がシーンに残ると、今の値を変更しても見えている物体が別になる。
        // ここで旧プレビューを破棄して、現在のコントローラが唯一の描画元になるようにする。
        var oldPreview = GameObject.Find("IntimidateRetreatPreview");
        if (oldPreview != null)
        {
            DestroyImmediate(oldPreview);
        }

        var oldPreviewBorder = GameObject.Find("IntimidateRetreatPreviewBorder");
        if (oldPreviewBorder != null)
        {
            DestroyImmediate(oldPreviewBorder);
        }

        retreatPreviewTexture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        retreatPreviewTexture.SetPixel(0, 0, Color.white);
        retreatPreviewTexture.Apply();
        retreatPreviewSprite = Sprite.Create(
            retreatPreviewTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);

        // ここが重要: 赤のハイライトは FieldGridConfig / Grid のワールド空間に属している。
        // その親に合わせないと、セル座標を UI のローカル座標のように扱って小さく、あるいは見えなくなる。
        retreatPreviewObject = new GameObject("IntimidateRetreatPreview");
        retreatPreviewRenderer = retreatPreviewObject.AddComponent<SpriteRenderer>();
        retreatPreviewRenderer.sprite = retreatPreviewSprite;
        retreatPreviewRenderer.material = new Material(Shader.Find("Sprites/Default"));
        retreatPreviewRenderer.sortingOrder = 45;
        retreatPreviewRenderer.enabled = false;

        retreatPreviewBorderObject = new GameObject("IntimidateRetreatPreviewBorder");
        retreatPreviewBorderRenderer = retreatPreviewBorderObject.AddComponent<LineRenderer>();
        retreatPreviewBorderRenderer.alignment = LineAlignment.View;
        retreatPreviewBorderRenderer.useWorldSpace = true;
        retreatPreviewBorderRenderer.loop = true;
        retreatPreviewBorderRenderer.positionCount = 4;
        retreatPreviewBorderRenderer.material = new Material(Shader.Find("Sprites/Default"));
        retreatPreviewBorderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        retreatPreviewBorderRenderer.receiveShadows = false;
        retreatPreviewBorderRenderer.textureMode = LineTextureMode.Stretch;
        retreatPreviewBorderRenderer.startWidth = retreatPreviewBorderThickness;
        retreatPreviewBorderRenderer.endWidth = retreatPreviewBorderThickness;
        retreatPreviewBorderRenderer.startColor = retreatPreviewBorderColor;
        retreatPreviewBorderRenderer.endColor = retreatPreviewBorderColor;
        retreatPreviewBorderRenderer.sortingOrder = 46;
        retreatPreviewBorderRenderer.enabled = false;

        EnsurePreviewParent();
    }

    private void EnsurePreviewParent()
    {
        Transform targetParent = null;

        if (FieldGridConfig.Instance != null && FieldGridConfig.Instance.transform != null)
        {
            targetParent = FieldGridConfig.Instance.transform;
        }
        else
        {
            var pathVisualization = FindFirstObjectByType<AnimalPathVisualization>();
            if (pathVisualization != null)
            {
                targetParent = pathVisualization.transform;
            }
        }

        if (targetParent == null)
        {
            return;
        }

        if (retreatPreviewObject != null && retreatPreviewObject.transform.parent != targetParent)
        {
            retreatPreviewObject.transform.SetParent(targetParent, worldPositionStays: false);
        }

        if (retreatPreviewBorderObject != null && retreatPreviewBorderObject.transform.parent != targetParent)
        {
            retreatPreviewBorderObject.transform.SetParent(targetParent, worldPositionStays: false);
        }
    }

    private void Start()
    {
        Debug.Log("威嚇: IntimidateActionController.Start が実行されました。");

        if (intimidateButton != null)
        {
            // InspectorですでにOnClickが配線済みなら二重実行を防ぐため追加しない。
            int persistentCount = intimidateButton.onClick.GetPersistentEventCount();
            if (persistentCount == 0)
            {
                intimidateButton.onClick.AddListener(OnClickIntimidateButton);
                runtimeClickListenerAdded = true;
                Debug.Log("威嚇: OnClickリスナーをコードから自動登録しました。");
            }
            else
            {
                runtimeClickListenerAdded = false;
                Debug.Log($"威嚇: Inspector配線を使用します (persistent listeners: {persistentCount})");
            }
        }
        else
        {
            Debug.LogWarning("威嚇: intimidateButton が未設定です。");
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnMoneyChanged.AddListener(OnMoneyChanged);
        }

        EnsurePreviewParent();
        ApplyModeVisual(false);
        RefreshButtonState();
    }

    private void OnDestroy()
    {
        if (intimidateButton != null && runtimeClickListenerAdded)
        {
            intimidateButton.onClick.RemoveListener(OnClickIntimidateButton);
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnMoneyChanged.RemoveListener(OnMoneyChanged);
        }

        if (retreatPreviewObject != null)
        {
            Destroy(retreatPreviewObject);
        }
        if (retreatPreviewBorderObject != null)
        {
            Destroy(retreatPreviewBorderObject);
        }
        if (retreatPreviewSprite != null)
        {
            Destroy(retreatPreviewSprite);
        }
        if (retreatPreviewTexture != null)
        {
            Destroy(retreatPreviewTexture);
        }

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!isAiming)
        {
            HideRetreatPreview();
            return;
        }

        if (CardPlacementController.Instance != null && CardPlacementController.Instance.HasSelection)
        {
            CancelAiming();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            CancelAiming();
            return;
        }

        UpdateRetreatPreview();

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            TryExecuteAtPointer();
        }
    }

    /// <summary>
    /// UI ButtonのOnClickから直接呼んでもよい。
    /// </summary>
    public void OnClickIntimidateButton()
    {
        Debug.Log("威嚇: ボタンがクリックされました。");

        if (isAiming)
        {
            CancelAiming();
            return;
        }

        if (!CanStartIntimidate())
        {
            if (EconomyManager.Instance != null)
            {
                Debug.Log($"威嚇: 所持金不足（必要: {intimidateCost}、所持: {EconomyManager.Instance.CurrentMoney}）");
            }
            else
            {
                Debug.LogWarning("威嚇: EconomyManager が見つからないため使用できません。");
            }
            return;
        }

        CardPlacementController.Instance?.ClearSelection();

        isAiming = true;
        ApplyModeVisual(true);
    }

    public void CancelAiming()
    {
        if (!isAiming) return;

        isAiming = false;
        HideRetreatPreview();
        ApplyModeVisual(false);
    }

    private void TryExecuteAtPointer()
    {
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null) return;

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        Vector3 mouseWorld = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        if ((!TryGetAnimalFromPointer(mouseWorld, out var animal) &&
             !TryGetAnimalByCollider(mouseWorld, out animal)) ||
            animal == null)
        {
            Debug.Log("威嚇: 動物が選択されていません。動物のいるマスをクリックしてください。");
            return;
        }

        if (!animal.TryGetRetreatCell(out var retreatCell))
        {
            Debug.Log($"威嚇: {animal.Stats.animalName} は後退できませんでした。");
            return;
        }

        if (EconomyManager.Instance != null && intimidateCost > 0)
        {
            if (!EconomyManager.Instance.TrySpend(intimidateCost))
            {
                Debug.LogWarning("威嚇: 実行時のコスト支払いに失敗したため、ターン進行を中止しました。");
                return;
            }
        }

        if (!animal.TryMoveToCell(retreatCell))
        {
            Debug.LogWarning("威嚇: 後退先への移動に失敗しました。");
            return;
        }

        animal.ApplyIntimidationForNextTurn();

        CancelAiming();
        TurnManager.Instance?.AdvanceTurn();
        RefreshButtonState();
    }

    private void UpdateRetreatPreview()
    {
        if (IsPointerOverUI())
        {
            HideRetreatPreview();
            return;
        }

        EnsurePreviewParent();

        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            HideRetreatPreview();
            return;
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null)
        {
            HideRetreatPreview();
            return;
        }

        Vector3 mouseWorld = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        if ((!TryGetAnimalFromPointer(mouseWorld, out var animal) &&
             !TryGetAnimalByCollider(mouseWorld, out animal)) ||
            animal == null ||
            !animal.TryGetRetreatCell(out var retreatCell))
        {
            HideRetreatPreview();
            return;
        }

        ShowRetreatPreview(retreatCell);
    }

    private void ShowRetreatPreview(Vector3Int cell)
    {
        if (retreatPreviewObject == null || retreatPreviewRenderer == null ||
            retreatPreviewBorderObject == null || retreatPreviewBorderRenderer == null ||
            FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            return;
        }

        Vector3 cellCenter = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        cellCenter.z = -0.2f;

        retreatPreviewObject.transform.position = cellCenter;
        retreatPreviewBorderObject.transform.position = cellCenter;

        Vector3 cellSize = FieldGridConfig.Instance.grid.cellSize;
        float scale = 4.6f;
        retreatPreviewObject.transform.localScale = new Vector3(
            cellSize.x * scale,
            cellSize.y * scale,
            1f);

        retreatPreviewRenderer.enabled = true;
        retreatPreviewRenderer.color = retreatPreviewColor;
        retreatPreviewRenderer.sortingOrder = 45;

        retreatPreviewBorderRenderer.enabled = true;
        retreatPreviewBorderRenderer.startWidth = retreatPreviewBorderThickness;
        retreatPreviewBorderRenderer.endWidth = retreatPreviewBorderThickness;
        retreatPreviewBorderRenderer.startColor = retreatPreviewBorderColor;
        retreatPreviewBorderRenderer.endColor = retreatPreviewBorderColor;
        retreatPreviewBorderRenderer.sortingOrder = 46;

        float halfWidth = cellSize.x * scale * 0.5f;
        float halfHeight = cellSize.y * scale * 0.5f;
        float borderZ = cellCenter.z - 0.01f;

        // 赤の nextCellHighlight と同じ四角枠の描画順序と頂点構成を採用する。
        retreatPreviewBorderRenderer.SetPosition(0, new Vector3(cellCenter.x - halfWidth, cellCenter.y - halfHeight, borderZ));
        retreatPreviewBorderRenderer.SetPosition(1, new Vector3(cellCenter.x - halfWidth, cellCenter.y + halfHeight, borderZ));
        retreatPreviewBorderRenderer.SetPosition(2, new Vector3(cellCenter.x + halfWidth, cellCenter.y + halfHeight, borderZ));
        retreatPreviewBorderRenderer.SetPosition(3, new Vector3(cellCenter.x + halfWidth, cellCenter.y - halfHeight, borderZ));
    }

    private void HideRetreatPreview()
    {
        if (retreatPreviewObject != null && retreatPreviewRenderer != null)
        {
            retreatPreviewRenderer.enabled = false;
        }
        if (retreatPreviewBorderObject != null && retreatPreviewBorderRenderer != null)
        {
            retreatPreviewBorderRenderer.enabled = false;
        }
    }

    private bool TryGetAnimalFromPointer(Vector3 mouseWorld, out AnimalController animal)
    {
        animal = null;

        if (FieldGridConfig.Instance == null ||
            FieldGridConfig.Instance.grid == null ||
            AnimalOccupancyMap.Instance == null)
        {
            return false;
        }

        // まずセル一致で取得する。
        Vector3Int cell = FieldGridConfig.Instance.grid.WorldToCell(mouseWorld);
        if (AnimalOccupancyMap.Instance.TryGetAnimalAt(cell, out animal) && animal != null)
        {
            Debug.Log($"威嚇: Grid一致で対象選択 ({animal.Stats.animalName}) cell={cell}");
            return true;
        }

        // スプライトの見た目をクリックした時のズレを吸収するため、近傍セルも許容する。
        const int nearRadius = 1;
        float bestDistance = float.MaxValue;
        AnimalController bestAnimal = null;

        for (int dx = -nearRadius; dx <= nearRadius; dx++)
        {
            for (int dy = -nearRadius; dy <= nearRadius; dy++)
            {
                var nearCell = new Vector3Int(cell.x + dx, cell.y + dy, cell.z);
                if (!AnimalOccupancyMap.Instance.TryGetAnimalAt(nearCell, out var candidate) || candidate == null)
                {
                    continue;
                }

                Vector3 candidateWorld = FieldGridConfig.Instance.grid.GetCellCenterWorld(candidate.CurrentCell);
                float sqrDist = (candidateWorld - mouseWorld).sqrMagnitude;
                if (sqrDist < bestDistance)
                {
                    bestDistance = sqrDist;
                    bestAnimal = candidate;
                }
            }
        }

        if (bestAnimal == null) return false;

        animal = bestAnimal;
        Debug.Log($"威嚇: 近傍セルで対象選択 ({animal.Stats.animalName}) clickCell={cell} animalCell={animal.CurrentCell}");
        return true;
    }

    private bool TryGetAnimalByCollider(Vector3 mouseWorld, out AnimalController animal)
    {
        animal = null;

        // Occupancyと見た目が一時的にズレるケース向けの最終フォールバック。
        var hits = Physics2D.OverlapPointAll(mouseWorld);
        if (hits == null || hits.Length == 0) return false;

        float bestDistance = float.MaxValue;
        AnimalController best = null;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            var candidate = hit.GetComponentInParent<AnimalController>();
            if (candidate == null) continue;

            float sqrDist = ((Vector2)candidate.transform.position - (Vector2)mouseWorld).sqrMagnitude;
            if (sqrDist < bestDistance)
            {
                bestDistance = sqrDist;
                best = candidate;
            }
        }

        if (best == null) return false;

        animal = best;
        Debug.Log($"威嚇: Colliderで対象選択 ({animal.Stats.animalName}) animalCell={animal.CurrentCell}");
        return true;
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool CanStartIntimidate()
    {
        if (EconomyManager.Instance == null) return false;
        if (intimidateCost <= 0) return true;

        return EconomyManager.Instance.CurrentMoney >= intimidateCost;
    }

    private void OnMoneyChanged(int _)
    {
        RefreshButtonState();
    }

    private void RefreshButtonState()
    {
        if (intimidateButton == null) return;

        intimidateButton.interactable = CanStartIntimidate() || isAiming;
    }

    private void ApplyModeVisual(bool aiming)
    {
        if (aimingModeIndicator != null)
        {
            aimingModeIndicator.SetActive(aiming);
        }

        if (buttonImage != null)
        {
            buttonImage.color = aiming ? aimingButtonColor : normalButtonColor;
        }
    }
}
