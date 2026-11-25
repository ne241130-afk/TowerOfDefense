using UnityEngine;

public static class MultiplayerPlayAreaUtility
{
    public static bool IsCellAllowedForPlayer(Vector3Int cell, int playerIndex)
    {
        // The whole board is shared for placement. Keep rejecting invalid player
        // indices; the walkable-cell check remains in the placement handler.
        return playerIndex == 0 || playerIndex == 1;
    }
}
