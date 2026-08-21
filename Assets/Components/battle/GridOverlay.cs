using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FieldGridConfig.walkableTilemap のセル境界線をゲームビューに描画するオーバーレイ。
/// LineRenderer を使うため URP/Built-in 問わず動作する。
///
/// セットアップ:
///   シーン内の任意のGameObjectにアタッチするだけで動作する。
///   FieldGridConfig がシーンに存在していること。
/// </summary>
public class GridOverlay : MonoBehaviour
{
    [Header("表示設定")]
    [Tooltip("グリッド線の色と透明度")]
    public Color lineColor = new Color(1f, 1f, 1f, 0.25f);

    [Tooltip("グリッド線の太さ(ワールド単位)")]
    public float lineWidth = 0.04f;

    [Tooltip("グリッド線の描画順序(Sorting Order)")]
    public int sortingOrder = 10;

    private readonly List<GameObject> lineObjects = new List<GameObject>();
    private Material lineMaterial;

    private void Start()
    {
        if (FieldGridConfig.Instance == null
            || FieldGridConfig.Instance.grid == null
            || FieldGridConfig.Instance.walkableTilemap == null)
        {
            Debug.LogWarning("GridOverlay: FieldGridConfig が見つかりません。", this);
            return;
        }

        // Sprites/Default は URP でも動作する汎用スプライトシェーダー
        lineMaterial = new Material(Shader.Find("Sprites/Default"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        BuildLines();
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    /// <summary>
    /// タイルマップの境界から行・列分の LineRenderer を生成する。
    /// </summary>
    private void BuildLines()
    {
        var tilemap = FieldGridConfig.Instance.walkableTilemap;
        var grid    = FieldGridConfig.Instance.grid;

        tilemap.CompressBounds();
        var bounds = tilemap.cellBounds;

        // 垂直線(各列の左端)
        for (int x = bounds.xMin; x <= bounds.xMax; x++)
        {
            Vector3 start = grid.CellToWorld(new Vector3Int(x, bounds.yMin, 0));
            Vector3 end   = grid.CellToWorld(new Vector3Int(x, bounds.yMax, 0));
            CreateLine($"VLine_{x}", start, end);
        }

        // 水平線(各行の下端)
        for (int y = bounds.yMin; y <= bounds.yMax; y++)
        {
            Vector3 start = grid.CellToWorld(new Vector3Int(bounds.xMin, y, 0));
            Vector3 end   = grid.CellToWorld(new Vector3Int(bounds.xMax, y, 0));
            CreateLine($"HLine_{y}", start, end);
        }
    }

    /// <summary>
    /// 2点間を結ぶ LineRenderer を生成して子オブジェクトとして追加する。
    /// </summary>
    private void CreateLine(string goName, Vector3 start, Vector3 end)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, worldPositionStays: true);
        lineObjects.Add(go);

        var lr = go.AddComponent<LineRenderer>();
        lr.material          = lineMaterial;
        lr.startColor        = lineColor;
        lr.endColor          = lineColor;
        lr.startWidth        = lineWidth;
        lr.endWidth          = lineWidth;
        lr.positionCount     = 2;
        lr.useWorldSpace     = true;
        lr.sortingOrder      = sortingOrder;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;

        // z=0 に固定して 2D カメラに確実に映るようにする
        start.z = 0f;
        end.z   = 0f;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }
}
