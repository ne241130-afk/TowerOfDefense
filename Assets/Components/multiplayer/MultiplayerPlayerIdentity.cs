using Mirror;
using UnityEngine;

public class MultiplayerPlayerIdentity : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnPlayerIndexChanged))]
    private int playerIndex = -1;

    private int latestPlayerIndex = -1;

    public int PlayerIndex => playerIndex;

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
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        latestPlayerIndex = playerIndex;
        TryBindLocalPlayer();
    }

    private void OnPlayerIndexChanged(int _, int newValue)
    {
        latestPlayerIndex = newValue;
        TryBindLocalPlayer();
    }

    private void TryBindLocalPlayer()
    {
        // Hookはサーバー側でも走るため、クライアント準備前には触らない。
        if (!NetworkClient.active) return;
        if (!IsOwnedSafe()) return;

        MultiplayerRuntime.SetLocalPlayer(this, latestPlayerIndex);
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

    [Mirror.ClientRpc]
    public void RpcApplyPrivateState(string handCsv, int money)
    {
        if (!IsOwnedSafe()) return;
        MultiplayerMatchCoordinator.ApplyLocalPrivateState(handCsv, money);
    }

    [Mirror.ClientRpc]
    public void RpcSetStatus(string message)
    {
        if (!IsOwnedSafe()) return;
        MultiplayerRuntime.SetStatus(message);
    }
}
