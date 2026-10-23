using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 壁セル(通行不可)を管理するシングルトン。
/// </summary>
public class WallBlockMap : MonoBehaviour
{
    public static WallBlockMap Instance { get; private set; }

    private readonly HashSet<Vector3Int> wallCells = new HashSet<Vector3Int>();

    public static void EnsureExists(Transform parent = null)
    {
        if (FindFirstObjectByType<WallBlockMap>() != null) return;

        var go = new GameObject(nameof(WallBlockMap));
        if (parent != null)
        {
            go.transform.SetParent(parent, worldPositionStays: false);
        }
        go.AddComponent<WallBlockMap>();
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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsWall(Vector3Int cell)
    {
        return wallCells.Contains(cell);
    }

    public bool TryAddWall(Vector3Int cell)
    {
        return wallCells.Add(cell);
    }

    public void Clear()
    {
        wallCells.Clear();
    }

    public bool CanPlaceWall(Vector3Int wallCell, out string reason)
    {
        reason = null;

        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            reason = "壁: FieldGridConfig が見つかりません。";
            return false;
        }

        if (!IsBaseWalkable(wallCell))
        {
            reason = "壁: そのマスには配置できません。";
            return false;
        }

        if (IsWall(wallCell))
        {
            reason = "壁: そのマスには既に壁があります。";
            return false;
        }

        if (AnimalOccupancyMap.Instance != null && AnimalOccupancyMap.Instance.IsOccupied(wallCell))
        {
            reason = "壁: 動物がいるマスには配置できません。";
            return false;
        }

        if (!HasPathForAllStartsWithCandidateWall(wallCell))
        {
            reason = "壁: 出口までの経路がなくなるため配置できません。";
            return false;
        }

        return true;
    }

    private bool HasPathForAllStartsWithCandidateWall(Vector3Int candidateWall)
    {
        if (FieldGridConfig.Instance == null) return false;
        if (FieldGridConfig.Instance.goalCells == null || FieldGridConfig.Instance.goalCells.Count == 0) return false;

        var starts = CollectPathValidationStarts();

        foreach (var start in starts)
        {
            // 置こうとしている壁が開始地点そのものなら到達不能。
            if (start == candidateWall) return false;

            if (!IsBaseWalkable(start)) continue;

            List<Vector3Int> path = AStarPathfinder.FindPath(
                start,
                FieldGridConfig.Instance.goalCells,
                cell => IsWalkableForValidation(cell, candidateWall),
                _ => false,
                _ => 1f);

            if (path == null || path.Count == 0)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsWalkableForValidation(Vector3Int cell, Vector3Int candidateWall)
    {
        if (FieldGridConfig.Instance == null) return false;
        if (!IsBaseWalkable(cell)) return false;
        if (cell == candidateWall) return false;
        if (IsWall(cell)) return false;
        return true;
    }

    private bool IsBaseWalkable(Vector3Int cell)
    {
        if (FieldGridConfig.Instance == null ||
            FieldGridConfig.Instance.walkableTilemap == null ||
            FieldGridConfig.Instance.grid == null)
        {
            return false;
        }

        Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        return FieldGridConfig.Instance.walkableTilemap.HasTile(
            FieldGridConfig.Instance.walkableTilemap.WorldToCell(worldPos));
    }

    private HashSet<Vector3Int> CollectPathValidationStarts()
    {
        var starts = new HashSet<Vector3Int>();

        foreach (var spawner in AnimalSpawner.All)
        {
            foreach (var spawnCell in spawner.GetAllSpawnCells())
            {
                starts.Add(spawnCell);
            }
        }

        if (AnimalOccupancyMap.Instance != null)
        {
            foreach (var animal in AnimalOccupancyMap.Instance.All)
            {
                if (animal == null) continue;
                starts.Add(animal.CurrentCell);
            }
        }

        return starts;
    }
}
