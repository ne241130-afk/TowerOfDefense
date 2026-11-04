using Mirror;
using UnityEngine;

public class MultiplayerPlayerIdentity : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnPlayerIndexChanged))]
    private int playerIndex = -1;

    public int PlayerIndex => playerIndex;

    [Server]
    public void SetPlayerIndexServer(int value)
    {
        playerIndex = value;
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        MultiplayerRuntime.SetLocalPlayer(this, playerIndex);
    }

    private void OnPlayerIndexChanged(int _, int newValue)
    {
        if (isOwned)
        {
            MultiplayerRuntime.SetLocalPlayer(this, newValue);
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
        if (!isOwned) return;
        MultiplayerMatchCoordinator.ApplyLocalPrivateState(handCsv, money);
    }

    [Mirror.ClientRpc]
    public void RpcSetStatus(string message)
    {
        if (!isOwned) return;
        MultiplayerRuntime.SetStatus(message);
    }
}
