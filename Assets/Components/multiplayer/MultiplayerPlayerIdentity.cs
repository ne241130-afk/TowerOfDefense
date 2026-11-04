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
        if (netIdentity == null) return;
        if (!netIdentity.isClient) return;
        if (!netIdentity.isOwned) return;

        MultiplayerRuntime.SetLocalPlayer(this, latestPlayerIndex);
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
        if (netIdentity == null || !netIdentity.isOwned) return;
        MultiplayerMatchCoordinator.ApplyLocalPrivateState(handCsv, money);
    }

    [Mirror.ClientRpc]
    public void RpcSetStatus(string message)
    {
        if (netIdentity == null || !netIdentity.isOwned) return;
        MultiplayerRuntime.SetStatus(message);
    }
}
