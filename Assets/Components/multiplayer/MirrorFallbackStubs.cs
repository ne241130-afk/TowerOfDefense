#if !MIRROR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SyncVarAttribute : Attribute
    {
        public string hook;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CommandAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ServerAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ClientRpcAttribute : Attribute
    {
    }

    public class NetworkIdentity : MonoBehaviour
    {
    }

    public class NetworkConnection
    {
        public NetworkIdentity identity;
    }

    public class NetworkConnectionToClient : NetworkConnection
    {
        public void Disconnect()
        {
        }
    }

    public class NetworkBehaviour : MonoBehaviour
    {
        public bool isOwned;

        public virtual void OnStartServer()
        {
        }

        public virtual void OnStartClient()
        {
        }

        public virtual void OnStartAuthority()
        {
        }
    }

    public class Transport : MonoBehaviour
    {
    }

    public class TelepathyTransport : Transport
    {
    }

    public static class NetworkServer
    {
        public static bool active;
        public static readonly Dictionary<int, NetworkConnectionToClient> connections = new Dictionary<int, NetworkConnectionToClient>();

        public static void Spawn(GameObject go)
        {
        }

        public static void AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player)
        {
            if (conn == null || player == null) return;
            conn.identity = player.GetComponent<NetworkIdentity>();
        }
    }

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager singleton;

        public int maxConnections = 2;
        public bool dontDestroyOnLoad = true;
        public int numPlayers;
        public string networkAddress = "127.0.0.1";
        public Transport transport;

        public virtual void Awake()
        {
            singleton = this;
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public virtual void OnStartServer()
        {
        }

        public virtual void OnServerConnect(NetworkConnectionToClient conn)
        {
        }

        public virtual void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            numPlayers++;
        }

        public virtual void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            numPlayers = Mathf.Max(0, numPlayers - 1);
        }

        public virtual void OnServerSceneChanged(string sceneName)
        {
        }

        public virtual void OnClientConnect()
        {
        }

        public virtual void OnClientDisconnect()
        {
        }

        public virtual void StartHost()
        {
            NetworkServer.active = true;
        }

        public virtual void StartClient()
        {
        }

        public virtual void ServerChangeScene(string sceneName)
        {
        }
    }
}
#endif
