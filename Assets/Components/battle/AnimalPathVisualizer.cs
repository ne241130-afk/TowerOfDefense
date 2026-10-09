using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 動物ごとの予定経路と次ターンの移動先を盤面上に可視化する。
/// 描画は既存の AnimalController が返す経路プレビューのみを使い、
/// 実際の移動ロジックと表示がずれないようにする。
/// </summary>
public class AnimalPathVisualizer : MonoBehaviour
{
    private const string VisualizerObjectName = "AnimalPathVisualizer";

    public static AnimalPathVisualizer Instance { get; private set; }

    [Header("経路表示")]
    [SerializeField] private Color pathColor = new Color(1f, 0.1f, 0.1f, 0.9f);
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private int pathSortingOrder = 100;
    [SerializeField] private float pathOffsetRatio = 0.12f;

    [Header("次マスハイライト")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.1f, 0.1f, 0.28f);
    [SerializeField] private int highlightSortingOrder = 101;
    [SerializeField] private float highlightScale = 0.92f;

    [Header("点線")]
    [SerializeField] private float dashTileScale = 2.5f;

    private readonly List<GameObject> spawnedVisuals = new List<GameObject>();
    private TurnManager subscribedTurnManager;
    private FieldEffectMap subscribedFieldEffectMap;
    private WaveManager subscribedWaveManager;

    private Material dashedLineMaterial;
    private Texture2D dashTexture;
    private Sprite highlightSprite;
    private Texture2D highlightTexture;
    private bool isDirty = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        TryCreateInstance();
    }

    public static AnimalPathVisualizer EnsureAttached(FieldGridConfig fieldGrid)
    {
        if (fieldGrid == null) return null;

        if (fieldGrid.TryGetComponent<AnimalPathVisualizer>(out var existingOnField))
        {
            Instance = existingOnField;
            return existingOnField;
        }

        AnimalPathVisualizer visualizer = fieldGrid.gameObject.AddComponent<AnimalPathVisualizer>();
        Instance = visualizer;
        return visualizer;
    }

    public static void RequestRefresh()
    {
        if (!TryCreateInstance()) return;
        Instance.MarkDirty();
    }

    private static bool TryCreateInstance()
    {
        if (Instance != null) return true;

        FieldGridConfig fieldGrid = FieldGridConfig.Instance != null
            ? FieldGridConfig.Instance
            : FindFirstObjectByType<FieldGridConfig>();
        if (fieldGrid == null) return false;

        AnimalPathVisualizer existing = FindFirstObjectByType<AnimalPathVisualizer>();
        if (existing != null)
        {
            Instance = existing;
            return true;
        }

        return EnsureAttached(fieldGrid) != null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void LateUpdate()
    {
        HookDependencies();

        if (!isDirty) return;
        Rebuild();
    }

    private void OnDisable()
    {
        UnhookDependencies();
    }

    private void OnDestroy()
    {
        UnhookDependencies();
        ClearSpawnedVisuals();

        if (dashedLineMaterial != null) Destroy(dashedLineMaterial);
        if (dashTexture != null) Destroy(dashTexture);
        if (highlightSprite != null) Destroy(highlightSprite);
        if (highlightTexture != null) Destroy(highlightTexture);

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void MarkDirty()
    {
        isDirty = true;
    }

    private void HookDependencies()
    {
        if (TurnManager.Instance != subscribedTurnManager)
        {
            if (subscribedTurnManager != null)
            {
                subscribedTurnManager.TurnAdvanced -= HandleTurnAdvanced;
            }

            subscribedTurnManager = TurnManager.Instance;
            if (subscribedTurnManager != null)
            {
                subscribedTurnManager.TurnAdvanced += HandleTurnAdvanced;
            }
        }

        if (FieldEffectMap.Instance != subscribedFieldEffectMap)
        {
            if (subscribedFieldEffectMap != null)
            {
                subscribedFieldEffectMap.EffectsChanged -= HandleEffectsChanged;
            }

            subscribedFieldEffectMap = FieldEffectMap.Instance;
            if (subscribedFieldEffectMap != null)
            {
                subscribedFieldEffectMap.EffectsChanged += HandleEffectsChanged;
            }
        }

        if (WaveManager.Instance != subscribedWaveManager)
        {
            if (subscribedWaveManager != null)
            {
                subscribedWaveManager.OnWaveStarted.RemoveListener(HandleWaveStarted);
            }

            subscribedWaveManager = WaveManager.Instance;
            if (subscribedWaveManager != null)
            {
                subscribedWaveManager.OnWaveStarted.AddListener(HandleWaveStarted);
            }
        }
    }

    private void UnhookDependencies()
    {
        if (subscribedTurnManager != null)
        {
            subscribedTurnManager.TurnAdvanced -= HandleTurnAdvanced;
            subscribedTurnManager = null;
        }

        if (subscribedFieldEffectMap != null)
        {
            subscribedFieldEffectMap.EffectsChanged -= HandleEffectsChanged;
            subscribedFieldEffectMap = null;
        }

        if (subscribedWaveManager != null)
        {
            subscribedWaveManager.OnWaveStarted.RemoveListener(HandleWaveStarted);
            subscribedWaveManager = null;
        }
    }

    private void HandleTurnAdvanced()
    {
        MarkDirty();
    }

    private void HandleEffectsChanged()
    {
        MarkDirty();
    }

    private void HandleWaveStarted(int _)
    {
        MarkDirty();
    }

    private void Rebuild()
    {
        isDirty = false;
        ClearSpawnedVisuals();

        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null || AnimalOccupancyMap.Instance == null)
        {
            return;
        }

        EnsureResources();
        if (dashedLineMaterial == null || highlightSprite == null) return;

        var nextCellCounts = new Dictionary<Vector3Int, int>();
        foreach (AnimalController animal in AnimalOccupancyMap.Instance.All)
        {
            if (animal == null || !animal.HasInitializedCell) continue;

            if (animal.TryGetPlannedPath(out List<Vector3Int> path))
            {
                CreatePathLine(animal, path);
            }

            if (animal.TryGetNextMovePreview(out Vector3Int nextCell))
            {
                if (!nextCellCounts.ContainsKey(nextCell))
                {
                    nextCellCounts[nextCell] = 0;
                }

                nextCellCounts[nextCell]++;
            }
        }

        foreach (KeyValuePair<Vector3Int, int> entry in nextCellCounts)
        {
            CreateHighlight(entry.Key, entry.Value);
        }
    }

    private void EnsureResources()
    {
        if (dashTexture == null)
        {
            dashTexture = new Texture2D(2, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };
            dashTexture.SetPixel(0, 0, Color.white);
            dashTexture.SetPixel(1, 0, Color.clear);
            dashTexture.Apply();
        }

        if (dashedLineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            dashedLineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = dashTexture,
                color = pathColor
            };
            dashedLineMaterial.mainTextureScale = new Vector2(dashTileScale, 1f);
        }

        if (highlightSprite == null)
        {
            highlightTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            highlightTexture.SetPixel(0, 0, Color.white);
            highlightTexture.Apply();
            highlightSprite = Sprite.Create(highlightTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            highlightSprite.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    private void CreatePathLine(AnimalController animal, IReadOnlyList<Vector3Int> path)
    {
        var root = new GameObject($"AnimalPath_{animal.GetInstanceID()}");
        root.transform.SetParent(transform, worldPositionStays: false);
        spawnedVisuals.Add(root);

        var line = root.AddComponent<LineRenderer>();
        line.material = dashedLineMaterial;
        line.textureMode = LineTextureMode.Tile;
        line.alignment = LineAlignment.View;
        line.useWorldSpace = true;
        line.positionCount = path.Count;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = pathColor;
        line.endColor = pathColor;
        line.sortingLayerName = "Default";
        line.sortingOrder = pathSortingOrder;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        Vector3 offset = GetPathOffset(animal);
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 world = FieldGridConfig.Instance.grid.GetCellCenterWorld(path[i]) + offset;
            world.z = 0f;
            line.SetPosition(i, world);
        }
    }

    private void CreateHighlight(Vector3Int cell, int overlapCount)
    {
        var go = new GameObject($"AnimalNextCell_{cell.x}_{cell.y}");
        go.transform.SetParent(transform, worldPositionStays: false);
        go.transform.position = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        spawnedVisuals.Add(go);

        var spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = highlightSprite;

        Color color = highlightColor;
        color.a = Mathf.Min(0.7f, highlightColor.a + (overlapCount - 1) * 0.12f);
        spriteRenderer.color = color;
        spriteRenderer.sortingLayerName = "Default";
        spriteRenderer.sortingOrder = highlightSortingOrder;

        Vector3 cellSize = FieldGridConfig.Instance.grid.cellSize;
        go.transform.localScale = new Vector3(cellSize.x * highlightScale, cellSize.y * highlightScale, 1f);
    }

    private Vector3 GetPathOffset(AnimalController animal)
    {
        float magnitude = Mathf.Min(FieldGridConfig.Instance.grid.cellSize.x, FieldGridConfig.Instance.grid.cellSize.y) * pathOffsetRatio;
        switch (Mathf.Abs(animal.GetInstanceID()) % 8)
        {
            case 0: return new Vector3(magnitude, 0f, 0f);
            case 1: return new Vector3(-magnitude, 0f, 0f);
            case 2: return new Vector3(0f, magnitude, 0f);
            case 3: return new Vector3(0f, -magnitude, 0f);
            case 4: return new Vector3(magnitude, magnitude, 0f) * 0.7f;
            case 5: return new Vector3(magnitude, -magnitude, 0f) * 0.7f;
            case 6: return new Vector3(-magnitude, magnitude, 0f) * 0.7f;
            default: return new Vector3(-magnitude, -magnitude, 0f) * 0.7f;
        }
    }

    private void ClearSpawnedVisuals()
    {
        foreach (GameObject go in spawnedVisuals)
        {
            if (go != null) Destroy(go);
        }

        spawnedVisuals.Clear();
    }
}
