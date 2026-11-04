using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

public class MultiplayerNetworkManager : NetworkManager
{
    public static MultiplayerNetworkManager Instance { get; private set; }

    [Header("LAN Discovery")]
    public MultiplayerLanDiscovery lanDiscovery;

    [Header("シーン設定")]
    public string startSceneName = "StartScene";
    public string multiBattleSceneName = "MultiBattleScene";

    private bool changingToBattle;

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
        lanDiscovery?.StartHostResponder();
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

        GameObject player = new GameObject($"Player_{playerIndex + 1}");
        player.AddComponent<NetworkIdentity>();
        MultiplayerPlayerIdentity identity = player.AddComponent<MultiplayerPlayerIdentity>();
        identity.SetPlayerIndexServer(playerIndex);

        NetworkServer.AddPlayerForConnection(conn, player);

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

    public override void OnClientConnect()
    {
        base.OnClientConnect();

        if (NetworkClient.isConnected)
        {
            if (!NetworkClient.ready)
            {
                NetworkClient.Ready();
            }

            if (NetworkClient.localPlayer == null)
            {
                NetworkClient.AddPlayer();
            }
        }

        MultiplayerRuntime.EnterMultiplayerSession();
        MultiplayerRuntime.SetStatus("Connected. Waiting for opponent...");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        MultiplayerRuntime.LeaveMultiplayerSession("Disconnected");
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
}
