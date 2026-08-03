using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// グリッド上のシンプルなA*経路探索。
/// ・複数ゴール対応(最も近いゴールへの経路を返す)
/// ・他の動物がいるマスは通行不可として扱う(occupancy考慮)
/// ・各マスのコスト(costAt)が高いほど迂回されやすくなる
///   → 妨害効果(沼地・鎖など)がここに乗ることで「最短経路の阻害」を表現する
/// </summary>
public static class AStarPathfinder
{
    private class Node
    {
        public Vector3Int position;
        public Node parent;
        public float gCost;
        public float hCost;
        public float FCost => gCost + hCost;
    }

    private static readonly Vector3Int[] directions =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
    };

    /// <summary>
    /// startから、複数あるgoalsのうち到達可能かつ最短の経路を返す。
    /// 見つからなければnull。
    /// </summary>
    public static List<Vector3Int> FindPath(
        Vector3Int start,
        IReadOnlyList<Vector3Int> goals,
        Func<Vector3Int, bool> isWalkable,
        Func<Vector3Int, bool> isBlockedByOther,
        Func<Vector3Int, float> costAt)
    {
        if (goals == null || goals.Count == 0) return null;

        List<Vector3Int> best = null;

        foreach (var goal in goals)
        {
            var path = FindPathSingle(start, goal, isWalkable, isBlockedByOther, costAt);
            if (path != null && (best == null || path.Count < best.Count))
            {
                best = path;
            }
        }

        return best;
    }

    private static List<Vector3Int> FindPathSingle(
        Vector3Int start,
        Vector3Int goal,
        Func<Vector3Int, bool> isWalkable,
        Func<Vector3Int, bool> isBlockedByOther,
        Func<Vector3Int, float> costAt)
    {
        var openSet = new List<Node>();
        var allNodes = new Dictionary<Vector3Int, Node>();
        var closed = new HashSet<Vector3Int>();

        var startNode = new Node { position = start, gCost = 0f, hCost = Heuristic(start, goal) };
        openSet.Add(startNode);
        allNodes[start] = startNode;

        while (openSet.Count > 0)
        {
            // 小規模マップ想定の線形探索で最小FCostを取り出す
            Node current = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < current.FCost ||
                    (Mathf.Approximately(openSet[i].FCost, current.FCost) && openSet[i].hCost < current.hCost))
                {
                    current = openSet[i];
                }
            }

            if (current.position == goal)
            {
                return ReconstructPath(current);
            }

            openSet.Remove(current);
            closed.Add(current.position);

            foreach (var dir in directions)
            {
                Vector3Int neighborPos = current.position + dir;

                if (closed.Contains(neighborPos)) continue;
                if (!isWalkable(neighborPos)) continue;
                if (isBlockedByOther(neighborPos)) continue; // 他の動物がいるマスは通れない

                float moveCost = costAt(neighborPos);
                if (float.IsPositiveInfinity(moveCost)) continue; // 完全に通行不可な妨害効果

                float tentativeG = current.gCost + moveCost;

                if (!allNodes.TryGetValue(neighborPos, out Node neighborNode))
                {
                    neighborNode = new Node { position = neighborPos };
                    allNodes[neighborPos] = neighborNode;
                }

                bool inOpen = openSet.Contains(neighborNode);
                if (!inOpen || tentativeG < neighborNode.gCost)
                {
                    neighborNode.gCost = tentativeG;
                    neighborNode.hCost = Heuristic(neighborPos, goal);
                    neighborNode.parent = current;

                    if (!inOpen) openSet.Add(neighborNode);
                }
            }
        }

        return null; // 到達不可
    }

    private static float Heuristic(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static List<Vector3Int> ReconstructPath(Node endNode)
    {
        var path = new List<Vector3Int>();
        var current = endNode;
        while (current != null)
        {
            path.Add(current.position);
            current = current.parent;
        }
        path.Reverse();
        return path;
    }
}
