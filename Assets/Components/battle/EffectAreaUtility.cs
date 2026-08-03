using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カード効果の設置範囲(セルの集合)を計算するユーティリティ。
/// </summary>
public static class EffectAreaUtility
{
    /// <summary>
    /// centerを中心とした正方形の範囲を返す。
    /// radius=0 → 中心の1マスのみ
    /// radius=1 → 3x3
    /// radius=2 → 5x5 ...
    /// </summary>
    public static List<Vector3Int> GetSquareArea(Vector3Int center, int radius)
    {
        var cells = new List<Vector3Int>();
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                cells.Add(center + new Vector3Int(dx, dy, 0));
            }
        }
        return cells;
    }
}
