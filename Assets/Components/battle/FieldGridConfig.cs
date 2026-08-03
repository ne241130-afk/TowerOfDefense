using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// フィールドの共通情報(Grid・通行可能マス・ゴール地点)を一箇所にまとめるシングルトン。
/// シーンに1つ配置し、既存のGridと地面のTilemap、ゴールセルをInspectorで設定する。
/// </summary>
public class FieldGridConfig : MonoBehaviour
{
    public static FieldGridConfig Instance { get; private set; }

    [Header("参照")]
    public Grid grid;

    [Tooltip("動物が通行できる範囲を表すTilemap(スクリーンショットの緑の地面)")]
    public Tilemap walkableTilemap;

    [Header("ゴール地点")]
    [Tooltip("最上段中央の2マスなど、動物が目指すセル座標のリスト")]
    public List<Vector3Int> goalCells = new List<Vector3Int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public bool IsWalkable(Vector3Int cell)
    {
        if (walkableTilemap == null || grid == null) return false;
        // grid.WorldToCell() はGrid空間の座標を返すが、
        // Tilemap.HasTile() はTilemap自身のローカル空間の座標を期待する。
        // GridとTilemapのTransformが異なる場合（localPositionが0でない場合）に
        // そのまま渡すと座標系がずれるため、一度ワールド座標に戻してからTilemap空間へ変換する。
        Vector3 worldPos = grid.GetCellCenterWorld(cell);
        return walkableTilemap.HasTile(walkableTilemap.WorldToCell(worldPos));
    }
}
