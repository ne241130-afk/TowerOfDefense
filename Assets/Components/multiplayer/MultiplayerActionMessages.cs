using Mirror;
using UnityEngine;

public struct PlaceCardRequestMessage : NetworkMessage
{
    public string cardId;
    public int handIndex;
    public int x;
    public int y;
    public int z;
}

public struct SkipTurnRequestMessage : NetworkMessage
{
}

public struct ReloadHandRequestMessage : NetworkMessage
{
}

public struct PrivateStateMessage : NetworkMessage
{
    public string handCsv;
    public int money;
}

public struct StatusMessage : NetworkMessage
{
    public string message;
}

public struct HandsSyncMessage : NetworkMessage
{
    public string player0HandCsv;
    public string player1HandCsv;
}

public struct PlacementAppliedMessage : NetworkMessage
{
    public string cardId;
    public int x;
    public int y;
    public int z;
    public int actorPlayerIndex;
}

public struct AdvanceTurnMessage : NetworkMessage
{
}

public struct TurnCoverStateMessage : NetworkMessage
{
    public bool cover0Visible;
    public bool cover1Visible;
}

public struct AnimalSpawnMessage : NetworkMessage
{
    public int animalId;
    public string prefabName;
    public int x;
    public int y;
    public int z;
}

public struct AnimalStateSyncMessage : NetworkMessage
{
    // Each entry: id,prefabName,x,y,z,captureHits; entries separated by '|'.
    public string animalStates;
    public int currentWave;
    public int turnInWave;
    public int captureCount;
    public int escapeCount;
}

public static class MultiplayerActionClient
{
    public static bool TrySendPlaceCard(string cardId, int handIndex, Vector3Int cell, out string reason)
    {
        if (!NetworkClient.active || !NetworkClient.isConnected)
        {
            reason = "client inactive/disconnected";
            return false;
        }

        if (!NetworkClient.ready)
        {
            reason = "client not ready";
            return false;
        }

        PlaceCardRequestMessage msg = new PlaceCardRequestMessage
        {
            cardId = cardId,
            handIndex = handIndex,
            x = cell.x,
            y = cell.y,
            z = cell.z
        };

        NetworkClient.Send(msg);
        Debug.Log($"[TRACE][MsgClient] Sent PlaceCardRequestMessage cardId={cardId} handIndex={handIndex} cell=({cell.x},{cell.y},{cell.z})");
        reason = string.Empty;
        return true;
    }

    public static bool TrySendSkipTurn(out string reason)
    {
        if (!NetworkClient.active || !NetworkClient.isConnected)
        {
            reason = "client inactive/disconnected";
            return false;
        }

        if (!NetworkClient.ready)
        {
            reason = "client not ready";
            return false;
        }

        NetworkClient.Send(new SkipTurnRequestMessage());
        Debug.Log("[TRACE][MsgClient] Sent SkipTurnRequestMessage");
        reason = string.Empty;
        return true;
    }

    public static bool TrySendReloadHand(out string reason)
    {
        if (!NetworkClient.active || !NetworkClient.isConnected)
        {
            reason = "client inactive/disconnected";
            return false;
        }

        if (!NetworkClient.ready)
        {
            reason = "client not ready";
            return false;
        }

        NetworkClient.Send(new ReloadHandRequestMessage());
        Debug.Log("[TRACE][MsgClient] Sent ReloadHandRequestMessage");
        reason = string.Empty;
        return true;
    }
}
