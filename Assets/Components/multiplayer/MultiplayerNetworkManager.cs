using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;
using System.Collections.Generic;

public class MultiplayerNetworkManager : NetworkManager
{
    public static MultiplayerNetworkManager Instance { get; private set; }

    private const uint DynamicPlayerAssetId = 0x4D505031;
    private static bool playerSpawnHandlerRegistered;
    private static bool coordinatorSpawnHandlerRegistered;

    [Header("LAN Discovery")]
    public MultiplayerLanDiscovery lanDiscovery;

    [Header("シーン設定")]
    public string startSceneName = "StartScene";
    public string multiBattleSceneName = "MultiBattleScene";
    public string gameOverSceneName = "GameOverScene";
    public string gameClearSceneName = "GameClearScene";

    private bool changingToBattle;
    private bool changingToResult;

    public override void Awake()
    {
        if (transport == null)
        {
            transport = GetComponent<Transport>();
        }

        if (transport == null)
        {
            transport = gameObject.AddComponent<TelepathyTransport>();
        }

        maxConnections = 2;
        dontDestroyOnLoad = true;
        TryDisableAutoCreatePlayer();

        base.Awake();
        Instance = this;
        RegisterDynamicPlayerSpawnHandlerIfNeeded();
        RegisterDynamicCoordinatorSpawnHandlerIfNeeded();

        if (lanDiscovery == null)
        {
            lanDiscovery = GetComponent<MultiplayerLanDiscovery>();
        }

        if (lanDiscovery == null)
        {
            lanDiscovery = gameObject.AddComponent<MultiplayerLanDiscovery>();
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        changingToBattle = false;
        changingToResult = false;
        NetworkServer.RegisterHandler<PlaceCardRequestMessage>(OnServerPlaceCardRequestFromClient, false);
        NetworkServer.RegisterHandler<SkipTurnRequestMessage>(OnServerSkipTurnRequestFromClient, false);
        Debug.Log("[TRACE][NetMgr] OnStartServer and registered action handlers (auth=false)");
        lanDiscovery?.StartHostResponder();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        NetworkServer.UnregisterHandler<PlaceCardRequestMessage>();
        NetworkServer.UnregisterHandler<SkipTurnRequestMessage>();
        Debug.Log("[TRACE][NetMgr] Unregistered action message handlers on server stop");
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        if (numPlayers >= maxConnections)
        {
            conn.Disconnect();
            return;
        }

        base.OnServerConnect(conn);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (numPlayers >= maxConnections)
        {
            conn.Disconnect();
            return;
        }

        int playerIndex = numPlayers;

        GameObject player = CreateRuntimePlayerObject($"Player_{playerIndex + 1}");
        MultiplayerPlayerIdentity identity = player.GetComponent<MultiplayerPlayerIdentity>();
        identity.SetPlayerIndexServer(playerIndex);

        if (!NetworkServer.AddPlayerForConnection(conn, player, DynamicPlayerAssetId))
        {
            Destroy(player);
            return;
        }

        if (!changingToBattle && numPlayers == maxConnections)
        {
            changingToBattle = true;
            ServerChangeScene(multiBattleSceneName);
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);

        if (SceneManager.GetActiveScene().name == multiBattleSceneName)
        {
            if (NetworkServer.active)
            {
                ServerChangeScene(startSceneName);
            }
        }
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (sceneName == multiBattleSceneName)
        {
            MultiplayerMatchCoordinator.SpawnOnServer();
        }
    }

    [Server]
    private void OnServerPlaceCardRequestFromClient(NetworkConnectionToClient conn, PlaceCardRequestMessage msg)
    {
        try
        {
            Debug.Log($"[TRACE][NetMgr] recv PlaceCardRequestMessage cardId={msg.cardId} handIndex={msg.handIndex} cell=({msg.x},{msg.y},{msg.z}) connId={(conn == null ? -1 : conn.connectionId)}");

            MultiplayerPlayerIdentity sender = ResolveIdentityFromConnection(conn);
            if (sender == null)
            {
                Debug.LogWarning("[TRACE][NetMgr] place request rejected: sender identity not found");
                return;
            }

            int safeNetId = 0;
            try
            {
                safeNetId = (int)sender.netId;
            }
            catch
            {
                safeNetId = -1;
            }

            Debug.Log($"[TRACE][NetMgr] sender resolved netId={safeNetId} playerIndex={sender.PlayerIndex}");

            if (MultiplayerMatchCoordinator.Instance == null)
            {
                Debug.LogWarning("[TRACE][NetMgr] place request rejected: coordinator not ready");
                return;
            }

            string normalizedCardId = msg.cardId;
            if (string.IsNullOrWhiteSpace(normalizedCardId))
            {
                Debug.LogWarning("[TRACE][NetMgr] place request rejected: cardId is empty");
                return;
            }

            if (MultiplayerCardCatalog.Find(normalizedCardId) == null)
            {
                string trimmed = normalizedCardId.Trim();
                if (MultiplayerCardCatalog.Find(trimmed) != null)
                {
                    normalizedCardId = trimmed;
                    Debug.Log($"[TRACE][NetMgr] normalized cardId by trim: '{msg.cardId}' -> '{normalizedCardId}'");
                }
                else
                {
                    Debug.LogWarning($"[TRACE][NetMgr] cardId not found in catalog: {normalizedCardId}");
                }
            }

            MultiplayerMatchCoordinator.Instance.ServerHandlePlaceCard(sender, normalizedCardId, new Vector3Int(msg.x, msg.y, msg.z), msg.handIndex);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[TRACE][NetMgr] OnServerPlaceCardRequestFromClient exception: " + ex);
        }
    }

    [Server]
    private void OnServerSkipTurnRequestFromClient(NetworkConnectionToClient conn, SkipTurnRequestMessage _)
    {
        try
        {
            Debug.Log($"[TRACE][NetMgr] recv SkipTurnRequestMessage connId={(conn == null ? -1 : conn.connectionId)}");

            MultiplayerPlayerIdentity sender = ResolveIdentityFromConnection(conn);
            if (sender == null)
            {
                Debug.LogWarning("[TRACE][NetMgr] skip request rejected: sender identity not found");
                return;
            }

            if (MultiplayerMatchCoordinator.Instance == null)
            {
                Debug.LogWarning("[TRACE][NetMgr] skip request rejected: coordinator not ready");
                return;
            }

            MultiplayerMatchCoordinator.Instance.ServerHandleSkipTurn(sender);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[TRACE][NetMgr] OnServerSkipTurnRequestFromClient exception: " + ex);
        }
    }

    [Server]
    private static MultiplayerPlayerIdentity ResolveIdentityFromConnection(NetworkConnectionToClient conn)
    {
        if (conn == null) return null;

        if (conn.identity != null)
        {
            MultiplayerPlayerIdentity direct = conn.identity.GetComponent<MultiplayerPlayerIdentity>();
            if (direct != null) return direct;
        }

        foreach (NetworkIdentity ni in NetworkServer.spawned.Values)
        {
            if (ni == null) continue;
            if (ni.connectionToClient != conn) continue;

            MultiplayerPlayerIdentity identity = ni.GetComponent<MultiplayerPlayerIdentity>();
            if (identity != null) return identity;
        }

        return null;
    }

    [Server]
    public void ServerTransitionToGameOver()
    {
        if (changingToResult) return;
        changingToResult = true;
        ServerChangeScene(gameOverSceneName);
    }

    [Server]
    public void ServerTransitionToGameClear()
    {
        if (changingToResult) return;
        changingToResult = true;
        ServerChangeScene(gameClearSceneName);
    }

    public void RequestGameOverFromLocal()
    {
        if (NetworkServer.active)
        {
            ServerTransitionToGameOver();
            return;
        }

        MultiplayerRuntime.LocalPlayerIdentity?.CmdRequestGameOver();
    }

    public void RequestGameClearFromLocal()
    {
        if (NetworkServer.active)
        {
            ServerTransitionToGameClear();
            return;
        }

        MultiplayerRuntime.LocalPlayerIdentity?.CmdRequestGameClear();
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();

        EnsureClientReadyAndPlayer("OnClientConnect");

        MultiplayerRuntime.EnterMultiplayerSession();
        MultiplayerRuntime.SetStatus("Connected. Waiting for opponent...");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        NetworkClient.RegisterHandler<PrivateStateMessage>(OnClientPrivateStateMessage, false);
        NetworkClient.RegisterHandler<StatusMessage>(OnClientStatusMessage, false);
        NetworkClient.RegisterHandler<HandsSyncMessage>(OnClientHandsSyncMessage, false);
        NetworkClient.RegisterHandler<PlacementAppliedMessage>(OnClientPlacementAppliedMessage, false);
        NetworkClient.RegisterHandler<AdvanceTurnMessage>(OnClientAdvanceTurnMessage, false);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        NetworkClient.UnregisterHandler<PrivateStateMessage>();
        NetworkClient.UnregisterHandler<StatusMessage>();
        NetworkClient.UnregisterHandler<HandsSyncMessage>();
        NetworkClient.UnregisterHandler<PlacementAppliedMessage>();
        NetworkClient.UnregisterHandler<AdvanceTurnMessage>();
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();
        EnsureClientReadyAndPlayer("OnClientSceneChanged");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        MultiplayerRuntime.LeaveMultiplayerSession("Disconnected");
    }

    public void EnsureLocalPlayerClientSide(string reason)
    {
        EnsureClientReadyAndPlayer(reason);
    }

    private static void EnsureClientReadyAndPlayer(string reason)
    {
        if (!NetworkClient.active || !NetworkClient.isConnected)
        {
            Debug.Log($"[TRACE][NetMgr] Skip EnsureClientReadyAndPlayer: inactive/disconnected reason={reason}");
            return;
        }

        NetworkConnection conn = NetworkClient.connection;
        if (conn != null && conn.identity != null)
        {
            Debug.Log($"[TRACE][NetMgr] connection.identity exists netId={conn.identity.netId} reason={reason}");
            return;
        }

        if (NetworkClient.localPlayer != null)
        {
            Debug.Log($"[TRACE][NetMgr] localPlayer exists netId={NetworkClient.localPlayer.netId} reason={reason}");
            return;
        }

        if (!NetworkClient.ready)
        {
            NetworkClient.Ready();
            Debug.Log($"[TRACE][NetMgr] NetworkClient.Ready() sent reason={reason}");
        }

        NetworkClient.AddPlayer();
        Debug.Log($"[TRACE][NetMgr] NetworkClient.AddPlayer() sent reason={reason}");
    }

    private static void OnClientPrivateStateMessage(PrivateStateMessage msg)
    {
        MultiplayerMatchCoordinator.ApplyLocalPrivateState(msg.handCsv, msg.money);
    }

    private static void OnClientStatusMessage(StatusMessage msg)
    {
        MultiplayerRuntime.SetStatus(msg.message);
    }

    private static void OnClientHandsSyncMessage(HandsSyncMessage msg)
    {
        if (CardDeckManager.Instance == null) return;

        List<CardData> player0Cards = ParseCardList(msg.player0HandCsv);
        List<CardData> player1Cards = ParseCardList(msg.player1HandCsv);
        CardDeckManager.Instance.SetBothHandsFromNetwork(player0Cards, player1Cards);
    }

    private static void OnClientPlacementAppliedMessage(PlacementAppliedMessage msg)
    {
        CardData card = MultiplayerCardCatalog.Find(msg.cardId);
        if (card == null)
        {
            MultiplayerRuntime.SetStatus("Card sync error: " + msg.cardId);
            return;
        }

        if (CardPlacementController.Instance == null)
        {
            MultiplayerRuntime.SetStatus("Placement controller not found");
            return;
        }

        bool clearSelection = MultiplayerRuntime.LocalPlayerIndex == msg.actorPlayerIndex;
        CardPlacementController.Instance.ApplyPlacementFromNetwork(card, new Vector3Int(msg.x, msg.y, msg.z), clearSelection);
    }

    private static void OnClientAdvanceTurnMessage(AdvanceTurnMessage _)
    {
        TurnManager.Instance?.AdvanceTurn();
    }

    private static List<CardData> ParseCardList(string handCsv)
    {
        List<CardData> cards = new List<CardData>();
        if (string.IsNullOrEmpty(handCsv)) return cards;

        string[] ids = handCsv.Split('|');
        foreach (string id in ids)
        {
            CardData card = MultiplayerCardCatalog.Find(id);
            if (card != null)
            {
                cards.Add(card);
            }
        }

        return cards;
    }

    private void TryDisableAutoCreatePlayer()
    {
        // Mirror version differences: some versions expose autoCreatePlayer, others do not.
        FieldInfo field = typeof(NetworkManager).GetField("autoCreatePlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(this, false);
            return;
        }

        PropertyInfo property = typeof(NetworkManager).GetProperty("autoCreatePlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
        {
            property.SetValue(this, false);
        }
    }

    private static void RegisterDynamicPlayerSpawnHandlerIfNeeded()
    {
        if (playerSpawnHandlerRegistered) return;

        NetworkClient.RegisterSpawnHandler(
            DynamicPlayerAssetId,
            SpawnDynamicPlayer,
            UnspawnDynamicPlayer);

        playerSpawnHandlerRegistered = true;
    }

    private static void RegisterDynamicCoordinatorSpawnHandlerIfNeeded()
    {
        if (coordinatorSpawnHandlerRegistered) return;

        NetworkClient.RegisterSpawnHandler(
            MultiplayerMatchCoordinator.DynamicCoordinatorAssetId,
            SpawnDynamicCoordinator,
            UnspawnDynamicCoordinator);

        coordinatorSpawnHandlerRegistered = true;
    }

    private static GameObject SpawnDynamicPlayer(Vector3 position, uint assetId)
    {
        GameObject go = CreateRuntimePlayerObject("RemotePlayer");
        go.transform.position = position;
        return go;
    }

    private static void UnspawnDynamicPlayer(GameObject spawned)
    {
        if (spawned != null)
        {
            Destroy(spawned);
        }
    }

    private static GameObject SpawnDynamicCoordinator(Vector3 position, uint assetId)
    {
        GameObject go = MultiplayerMatchCoordinator.CreateRuntimeCoordinatorObject("RemoteMatchCoordinator");
        go.transform.position = position;
        return go;
    }

    private static void UnspawnDynamicCoordinator(GameObject spawned)
    {
        if (spawned != null)
        {
            Destroy(spawned);
        }
    }

    private static GameObject CreateRuntimePlayerObject(string objectName)
    {
        GameObject player = new GameObject(objectName);
        player.AddComponent<NetworkIdentity>();
        player.AddComponent<MultiplayerPlayerIdentity>();
        return player;
    }
}
