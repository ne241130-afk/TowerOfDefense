using UnityEngine;
using UnityEngine.Tilemaps;

public static class MultiplayerPlayAreaUtility
{
    public static bool IsCellAllowedForPlayer(Vector3Int cell, int playerIndex)
    {
        if (playerIndex != 0 && playerIndex != 1) return false;
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.walkableTilemap == null) return true;

        Tilemap map = FieldGridConfig.Instance.walkableTilemap;
        BoundsInt bounds = map.cellBounds;

        bool found = false;
        int minX = int.MaxValue;
        int maxX = int.MinValue;

        foreach (Vector3Int c in bounds.allPositionsWithin)
        {
            if (!map.HasTile(c)) continue;
            found = true;
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
        }

        if (!found) return true;

        float split = (minX + maxX + 1) * 0.5f;

        if (playerIndex == 0)
        {
            return cell.x < split;
        }

        return cell.x >= split;
    }
}
