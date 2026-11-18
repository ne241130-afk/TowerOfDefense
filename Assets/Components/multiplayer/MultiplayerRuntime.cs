using UnityEngine;

public static class MultiplayerRuntime
{
    public static bool IsMultiplayerSession { get; private set; }
    public static bool IsGameplayReady { get; private set; }
    public static int LocalPlayerIndex { get; private set; } = -1;
    public static MultiplayerPlayerIdentity LocalPlayerIdentity { get; private set; }

    public static string StatusMessage { get; private set; } = string.Empty;

    public static void EnterMultiplayerSession()
    {
        if (IsMultiplayerSession)
        {
            // Keep current readiness when duplicate enter calls happen during scene/network init.
            if (string.IsNullOrEmpty(StatusMessage))
            {
                StatusMessage = "Connecting...";
            }
            return;
        }

        IsMultiplayerSession = true;
        IsGameplayReady = false;
        StatusMessage = "Connecting...";
    }

    public static void LeaveMultiplayerSession(string message)
    {
        IsMultiplayerSession = false;
        IsGameplayReady = false;
        LocalPlayerIndex = -1;
        LocalPlayerIdentity = null;
        StatusMessage = message;
    }

    public static void SetLocalPlayer(MultiplayerPlayerIdentity identity)
    {
        if (identity == null) return;

        // Never accept a guessed or caller-cached index. Bind only an identity
        // Mirror associates with this client, and read its server-owned SyncVar.
        if (Mirror.NetworkClient.active)
        {
            try
            {
                Mirror.NetworkIdentity localPlayer = Mirror.NetworkClient.localPlayer;
                Mirror.NetworkIdentity connectionIdentity = Mirror.NetworkClient.connection != null
                    ? Mirror.NetworkClient.connection.identity
                    : null;
                bool isLocalPlayer = localPlayer != null && localPlayer.netId == identity.netId;
                bool isConnectionIdentity = connectionIdentity != null && connectionIdentity.netId == identity.netId;
                if (!isLocalPlayer && !isConnectionIdentity)
                {
                    return;
                }
            }
            catch
            {
                return;
            }
        }

        LocalPlayerIdentity = identity;
        int syncedIndex = identity.PlayerIndex;
        if (syncedIndex == 0 || syncedIndex == 1)
        {
            LocalPlayerIndex = syncedIndex;
            IsGameplayReady = true;
            return;
        }

        LocalPlayerIndex = -1;
        IsGameplayReady = false;
    }

    public static void MarkGameplayReady()
    {
        // The match may be ready before this client's player index has arrived.
        // Keep input gated until the authoritative identity is usable.
        if (IsMultiplayerSession && GetEffectiveLocalPlayerIndex() < 0) return;
        IsGameplayReady = true;
    }

    public static void SetStatus(string message)
    {
        StatusMessage = message;
    }

    public static int GetEffectiveLocalPlayerIndex()
    {
        if (Mirror.NetworkClient.active)
        {
            MultiplayerPlayerIdentity identity = null;
            try
            {
                var local = Mirror.NetworkClient.localPlayer;
                if (local != null)
                {
                    identity = local.GetComponent<MultiplayerPlayerIdentity>();
                }

                if (identity == null && Mirror.NetworkClient.connection != null && Mirror.NetworkClient.connection.identity != null)
                {
                    identity = Mirror.NetworkClient.connection.identity.GetComponent<MultiplayerPlayerIdentity>();
                }
            }
            catch
            {
            }

            if (identity != null && (identity.PlayerIndex == 0 || identity.PlayerIndex == 1))
            {
                LocalPlayerIdentity = identity;
                LocalPlayerIndex = identity.PlayerIndex;
                return LocalPlayerIndex;
            }

            // While the local identity or its SyncVar is still arriving, do not
            // reuse a stale cached value from an earlier identity/session.
            LocalPlayerIdentity = identity;
            LocalPlayerIndex = -1;
            return -1;
        }

        return LocalPlayerIndex == 0 || LocalPlayerIndex == 1 ? LocalPlayerIndex : -1;
    }

    public static bool IsPointerInsideOwnHalf()
    {
        if (!IsMultiplayerSession) return true;

        int playerIndex = GetEffectiveLocalPlayerIndex();
        if (playerIndex < 0)
        {
            // Do not allow full-board placement while the local player index is still unresolved.
            return false;
        }

        float normalized = Screen.width > 0 ? Input.mousePosition.x / Screen.width : 0.5f;
        if (playerIndex == 0)
        {
            // Host(Player0) controls RIGHT side.
            return normalized >= 0.5f;
        }

        // Guest(Player1) controls LEFT side.
        return normalized < 0.5f;
    }
}
