using Mirror;
using UnityEngine;

public class MultiplayerPlayerIdentity : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnPlayerIndexChanged))]
    private int playerIndex = -1;

    private int latestPlayerIndex = -1;
    private bool hasPendingPrivateState;
    private string pendingHandCsv;
    private int pendingMoney;

    public int PlayerIndex => playerIndex;

    public bool IsUsableLocalCommandSource(out string reason)
    {
        if (!NetworkClient.active)
        {
            reason = "client inactive";
            return false;
        }

        if (!NetworkClient.ready)
        {
            reason = "client not ready";
            return false;
        }

        if (NetworkClient.connection == null)
        {
            reason = "client connection missing";
            return false;
        }

        if (netId == 0)
        {
            reason = "identity not spawned";
            return false;
        }

        if (!IsLocalPlayerObject())
        {
            reason = "not local authority object";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    [Server]
    public void SetPlayerIndexServer(int value)
    {
        playerIndex = value;
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        latestPlayerIndex = playerIndex;
        TryBindLocalPlayer();
        TryApplyPendingPrivateState();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        latestPlayerIndex = playerIndex;
        TryBindLocalPlayer();
        TryApplyPendingPrivateState();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        latestPlayerIndex = playerIndex;
        TryBindLocalPlayer();
        TryApplyPendingPrivateState();
    }

    private void OnPlayerIndexChanged(int _, int newValue)
    {
        latestPlayerIndex = newValue;
        TryBindLocalPlayer();
        TryApplyPendingPrivateState();
    }

    private void TryBindLocalPlayer()
    {
        // Hookはサーバー側でも走るため、クライアント準備前には触らない。
        if (!NetworkClient.active) return;
        if (!IsLocalPlayerObject()) return;
        if (netId == 0) return;

        // playerIndex sync can arrive later; bind early to allow command path recovery.
        MultiplayerRuntime.SetLocalPlayer(this, latestPlayerIndex);
        if (latestPlayerIndex >= 0)
        {
            MultiplayerRuntime.MarkGameplayReady();
        }
    }

    private bool IsOwnedSafe()
    {
        try
        {
            return isOwned;
        }
        catch
        {
            return false;
        }
    }

    private bool IsLocalPlayerObject()
    {
        if (!NetworkClient.active) return false;

        try
        {
            NetworkIdentity localPlayer = NetworkClient.localPlayer;
            if (localPlayer != null && netIdentity != null && localPlayer.netId == netIdentity.netId)
            {
                return true;
            }
        }
        catch
        {
        }

        return IsOwnedSafe();
    }

    private void Update()
    {
        if (!MultiplayerRuntime.IsMultiplayerSession) return;

        if (MultiplayerRuntime.LocalPlayerIdentity == null || MultiplayerRuntime.LocalPlayerIndex < 0)
        {
            TryBindLocalPlayer();
        }

        if (hasPendingPrivateState)
        {
            TryApplyPendingPrivateState();
        }
    }

    private void TryApplyPendingPrivateState()
    {
        if (!hasPendingPrivateState) return;
        if (!IsLocalPlayerObject()) return;

        hasPendingPrivateState = false;
        MultiplayerMatchCoordinator.ApplyLocalPrivateState(pendingHandCsv, pendingMoney);
    }

    [Command]
    public void CmdRequestPlaceCard(string cardId, int x, int y, int z)
    {
        MultiplayerMatchCoordinator.Instance?.ServerHandlePlaceCard(this, cardId, new Vector3Int(x, y, z));
    }

    [Command]
    public void CmdRequestSkipTurn()
    {
        MultiplayerMatchCoordinator.Instance?.ServerHandleSkipTurn(this);
    }

    [Command]
    public void CmdRequestGameOver()
    {
        MultiplayerNetworkManager.Instance?.ServerTransitionToGameOver();
    }

    [Command]
    public void CmdRequestGameClear()
    {
        MultiplayerNetworkManager.Instance?.ServerTransitionToGameClear();
    }

    [Mirror.ClientRpc]
    public void RpcApplyPrivateState(string handCsv, int money)
    {
        pendingHandCsv = handCsv;
        pendingMoney = money;
        hasPendingPrivateState = true;
        TryApplyPendingPrivateState();
    }

    [Mirror.ClientRpc]
    public void RpcSetStatus(string message)
    {
        if (!IsLocalPlayerObject()) return;
        MultiplayerRuntime.SetStatus(message);
    }
}
