using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 動物ごとの予定経路と次ターンの移動先をグリッドに重ねて描画する。
/// </summary>
public class AnimalPathVisualization : MonoBehaviour
{
    private sealed class VisualState
    {
        public GameObject lineObject;
        public LineRenderer lineRenderer;
        public Material lineMaterial;
        public GameObject highlightObject;
        public SpriteRenderer highlightRenderer;
        public GameObject highlightBorderObject;
        public LineRenderer highlightBorderRenderer;
        public Material highlightBorderMaterial;
    }

    [Header("経路表示")]
    [Tooltip("経路の色。動物ごとに赤系の濃淡へ少しだけずらして使う。")]
    public Color routeColor = new Color(1f, 0.1f, 0.1f, 0.95f);

    [Tooltip("点線の太さ")]
    public float routeWidth = 0.84f;

    [Tooltip("点線1セットの長さ(ワールド単位)")]
    public float dashLength = 0.35f;

    [Tooltip("表示を少し手前に出すためのZオフセット")]
    public float zOffset = -0.25f;

    [Tooltip("LineRenderer の描画順")]
    public int routeSortingOrder = 50;

    [Header("次ターン移動先")]
    [Tooltip("次ターンに移動する予定マスの色")]
    public Color nextCellColor = new Color(1f, 0.15f, 0.15f, 0.28f);

    [Tooltip("ハイライトのセルに対する拡大率")]
    public float nextCellScale = 4.6f;

    [Tooltip("次ターン移動先ハイライトの外枠色")]
    public Color nextCellBorderColor = new Color(0.75f, 0.05f, 0.05f, 0.95f);

    [Tooltip("次ターン移動先ハイライトの外枠太さ")]
    public float nextCellBorderWidth = 0.5f;

    [Tooltip("ハイライトの描画順")]
    public int nextCellSortingOrder = 45;

    private readonly Dictionary<AnimalController, VisualState> visuals =
        new Dictionary<AnimalController, VisualState>();

    private Material baseLineMaterial;
    private Texture2D dashTexture;
    private Texture2D highlightTexture;
    private Sprite highlightSprite;

    public static void EnsureExists(Transform parent = null)
    {
        if (FindFirstObjectByType<AnimalPathVisualization>() != null) return;

        var go = new GameObject(nameof(AnimalPathVisualization));
        if (parent != null)
        {
            go.transform.SetParent(parent, worldPositionStays: false);
        }

        go.AddComponent<AnimalPathVisualization>();
    }

    private void Awake()
    {
        dashTexture = new Texture2D(2, 1, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };
        dashTexture.SetPixels(new[]
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 1f, 1f, 0f)
        });
        dashTexture.Apply();

        highlightTexture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        highlightTexture.SetPixel(0, 0, Color.white);
        highlightTexture.Apply();
        highlightSprite = Sprite.Create(
            highlightTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            baseLineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = dashTexture
            };
        }
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        foreach (var visual in visuals.Values)
        {
            DestroyVisualState(visual);
        }

        visuals.Clear();

        if (baseLineMaterial != null) Destroy(baseLineMaterial);
        if (highlightSprite != null) Destroy(highlightSprite);
        if (dashTexture != null) Destroy(dashTexture);
        if (highlightTexture != null) Destroy(highlightTexture);
    }

    private void Refresh()
    {
        if (FieldGridConfig.Instance == null ||
            FieldGridConfig.Instance.grid == null ||
            AnimalOccupancyMap.Instance == null)
        {
            DisableAllVisuals();
            return;
        }

        var activeAnimals = new HashSet<AnimalController>();
        int visualIndex = 0;

        foreach (var animal in AnimalOccupancyMap.Instance.All)
        {
            if (animal == null) continue;

            activeAnimals.Add(animal);

            if (!visuals.TryGetValue(animal, out var visual))
            {
                visual = CreateVisualState(animal);
                visuals.Add(animal, visual);
            }

            UpdateVisual(animal, visual, visualIndex);
            visualIndex++;
        }

        var staleAnimals = new List<AnimalController>();
        foreach (var pair in visuals)
        {
            if (!activeAnimals.Contains(pair.Key))
            {
                DestroyVisualState(pair.Value);
                staleAnimals.Add(pair.Key);
            }
        }

        foreach (var animal in staleAnimals)
        {
            visuals.Remove(animal);
        }
    }

    private VisualState CreateVisualState(AnimalController animal)
    {
        var state = new VisualState();

        state.lineObject = new GameObject($"{animal.name}_Path");
        state.lineObject.transform.SetParent(transform, worldPositionStays: false);
        state.lineRenderer = state.lineObject.AddComponent<LineRenderer>();
        state.lineRenderer.alignment = LineAlignment.View;
        state.lineRenderer.useWorldSpace = true;
        state.lineRenderer.textureMode = LineTextureMode.Tile;
        state.lineRenderer.numCapVertices = 2;
        state.lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        state.lineRenderer.receiveShadows = false;
        state.lineRenderer.positionCount = 0;
        state.lineRenderer.startWidth = routeWidth;
        state.lineRenderer.endWidth = routeWidth;

        if (baseLineMaterial != null)
        {
            state.lineMaterial = new Material(baseLineMaterial)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            state.lineRenderer.material = state.lineMaterial;
        }

        state.highlightObject = new GameObject($"{animal.name}_NextCell");
        state.highlightObject.transform.SetParent(transform, worldPositionStays: false);
        state.highlightRenderer = state.highlightObject.AddComponent<SpriteRenderer>();
        state.highlightRenderer.sprite = highlightSprite;

        state.highlightBorderObject = new GameObject($"{animal.name}_NextCellBorder");
        state.highlightBorderObject.transform.SetParent(transform, worldPositionStays: false);
        state.highlightBorderRenderer = state.highlightBorderObject.AddComponent<LineRenderer>();
        state.highlightBorderRenderer.alignment = LineAlignment.View;
        state.highlightBorderRenderer.useWorldSpace = true;
        state.highlightBorderRenderer.loop = true;
        state.highlightBorderRenderer.positionCount = 4;
        state.highlightBorderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        state.highlightBorderRenderer.receiveShadows = false;
        state.highlightBorderRenderer.textureMode = LineTextureMode.Stretch;
        state.highlightBorderRenderer.startWidth = nextCellBorderWidth;
        state.highlightBorderRenderer.endWidth = nextCellBorderWidth;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            state.highlightBorderMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            state.highlightBorderRenderer.material = state.highlightBorderMaterial;
        }

        return state;
    }

    private void UpdateVisual(AnimalController animal, VisualState visual, int visualIndex)
    {
        Color perAnimalRouteColor = GetPerAnimalColor(routeColor, animal.GetInstanceID(), 0.04f, 0.18f);
        Color perAnimalNextCellColor = GetPerAnimalColor(nextCellColor, animal.GetInstanceID(), 0.02f, 0.1f);

        UpdatePathLine(animal, visual, visualIndex, perAnimalRouteColor);
        UpdateNextCellHighlight(animal, visual, visualIndex, perAnimalNextCellColor);
    }

    private void UpdatePathLine(AnimalController animal, VisualState visual, int visualIndex, Color color)
    {
        if (!animal.TryGetPlannedPath(out var path) || path == null || path.Count < 2)
        {
            visual.lineRenderer.enabled = false;
            visual.lineRenderer.positionCount = 0;
            return;
        }

        float z = zOffset - visualIndex * 0.001f;
        float totalLength = 0f;
        Vector3 previous = default;

        visual.lineRenderer.enabled = true;
        visual.lineRenderer.positionCount = path.Count;
        visual.lineRenderer.sortingOrder = routeSortingOrder + visualIndex;
        visual.lineRenderer.startColor = color;
        visual.lineRenderer.endColor = color;
        visual.lineRenderer.startWidth = routeWidth;
        visual.lineRenderer.endWidth = routeWidth;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 position = FieldGridConfig.Instance.grid.GetCellCenterWorld(path[i]);
            position.z = z;
            visual.lineRenderer.SetPosition(i, position);

            if (i > 0)
            {
                totalLength += Vector3.Distance(previous, position);
            }

            previous = position;
        }

        if (visual.lineMaterial != null)
        {
            float repeatCount = totalLength / Mathf.Max(0.01f, dashLength);
            visual.lineMaterial.mainTextureScale = new Vector2(Mathf.Max(1f, repeatCount), 1f);
            visual.lineMaterial.color = Color.white;
        }
    }

    private void UpdateNextCellHighlight(AnimalController animal, VisualState visual, int visualIndex, Color color)
    {
        if (!animal.TryGetNextTurnDestination(out var nextCell))
        {
            visual.highlightRenderer.enabled = false;
            visual.highlightBorderRenderer.enabled = false;
            return;
        }

        Vector3 cellCenter = FieldGridConfig.Instance.grid.GetCellCenterWorld(nextCell);
        cellCenter.z = zOffset - 0.02f - visualIndex * 0.001f;

        visual.highlightObject.transform.position = cellCenter;
        Vector3 cellSize = FieldGridConfig.Instance.grid.cellSize;
        visual.highlightObject.transform.localScale = new Vector3(
            cellSize.x * nextCellScale,
            cellSize.y * nextCellScale,
            1f);

        visual.highlightRenderer.enabled = true;
        visual.highlightRenderer.color = color;
        visual.highlightRenderer.sortingOrder = nextCellSortingOrder + visualIndex;

        float halfWidth = cellSize.x * nextCellScale * 0.5f;
        float halfHeight = cellSize.y * nextCellScale * 0.5f;
        float borderZ = cellCenter.z - 0.01f;

        visual.highlightBorderRenderer.enabled = true;
        visual.highlightBorderRenderer.sortingOrder = nextCellSortingOrder + visualIndex + 1;
        visual.highlightBorderRenderer.startColor = nextCellBorderColor;
        visual.highlightBorderRenderer.endColor = nextCellBorderColor;
        visual.highlightBorderRenderer.startWidth = nextCellBorderWidth;
        visual.highlightBorderRenderer.endWidth = nextCellBorderWidth;
        visual.highlightBorderRenderer.SetPosition(0, new Vector3(cellCenter.x - halfWidth, cellCenter.y - halfHeight, borderZ));
        visual.highlightBorderRenderer.SetPosition(1, new Vector3(cellCenter.x - halfWidth, cellCenter.y + halfHeight, borderZ));
        visual.highlightBorderRenderer.SetPosition(2, new Vector3(cellCenter.x + halfWidth, cellCenter.y + halfHeight, borderZ));
        visual.highlightBorderRenderer.SetPosition(3, new Vector3(cellCenter.x + halfWidth, cellCenter.y - halfHeight, borderZ));
    }

    private static Color GetPerAnimalColor(Color baseColor, int seed, float minBoost, float maxBoost)
    {
        float normalized = Mathf.InverseLerp(0f, 6f, Mathf.Abs(seed % 7));
        float boost = Mathf.Lerp(minBoost, maxBoost, normalized);

        return new Color(
            baseColor.r,
            Mathf.Clamp01(baseColor.g + boost),
            Mathf.Clamp01(baseColor.b + boost * 0.7f),
            baseColor.a);
    }

    private void DisableAllVisuals()
    {
        foreach (var visual in visuals.Values)
        {
            if (visual.lineRenderer != null)
            {
                visual.lineRenderer.enabled = false;
                visual.lineRenderer.positionCount = 0;
            }

            if (visual.highlightRenderer != null)
            {
                visual.highlightRenderer.enabled = false;
            }

            if (visual.highlightBorderRenderer != null)
            {
                visual.highlightBorderRenderer.enabled = false;
            }
        }
    }

    private void DestroyVisualState(VisualState visual)
    {
        if (visual.lineObject != null) Destroy(visual.lineObject);
        if (visual.highlightObject != null) Destroy(visual.highlightObject);
        if (visual.highlightBorderObject != null) Destroy(visual.highlightBorderObject);
        if (visual.lineMaterial != null) Destroy(visual.lineMaterial);
        if (visual.highlightBorderMaterial != null) Destroy(visual.highlightBorderMaterial);
    }
}
