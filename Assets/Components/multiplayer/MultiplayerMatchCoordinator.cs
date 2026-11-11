using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class MultiplayerMatchCoordinator : NetworkBehaviour
{
    public const uint DynamicCoordinatorAssetId = 0x4D434331;

    private class ServerPlayerState
    {
        public readonly List<string> deck = new List<string>();
        public readonly List<string> hand = new List<string>();
        public int money;
    }

    public static MultiplayerMatchCoordinator Instance { get; private set; }

    public static event Action OnPublicStateChanged;

    [SyncVar(hook = nameof(OnMatchReadyChanged))]
    private bool matchReady;

    [SyncVar(hook = nameof(OnPublicValueChanged))] private int player0Money;
    [SyncVar(hook = nameof(OnPublicValueChanged))] private int player1Money;
    [SyncVar(hook = nameof(OnPublicValueChanged))] private int player0HandCount;
    [SyncVar(hook = nameof(OnPublicValueChanged))] private int player1HandCount;

    public int Player0Money => player0Money;
    public int Player1Money => player1Money;
    public int Player0HandCount => player0HandCount;
    public int Player1HandCount => player1HandCount;

    private readonly Dictionary<int, ServerPlayerState> playerStates = new Dictionary<int, ServerPlayerState>();
    private readonly Dictionary<int, MultiplayerPlayerIdentity> playerByIndex = new Dictionary<int, MultiplayerPlayerIdentity>();
    private readonly bool[] endedThisRound = new bool[2];

    private int initialMoney = 100;
    private int handSize = 4;
    private bool serverInitialized;

    public static void SpawnOnServer()
    {
        if (!NetworkServer.active) return;
        if (Instance != null) return;

        GameObject go = CreateRuntimeCoordinatorObject("MultiplayerMatchCoordinator");
        NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
        MultiplayerMatchCoordinator coordinator = go.GetComponent<MultiplayerMatchCoordinator>();
        NetworkServer.Spawn(go, DynamicCoordinatorAssetId);

        if (identity == null || coordinator == null)
        {
            Debug.LogError("MultiplayerMatchCoordinator: spawn failed.");
        }
    }

    public static GameObject CreateRuntimeCoordinatorObject(string objectName)
    {
        GameObject go = new GameObject(objectName);
        go.AddComponent<NetworkIdentity>();
        go.AddComponent<MultiplayerMatchCoordinator>();
        return go;
    }

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        InitializeServerState();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        MultiplayerRuntime.EnterMultiplayerSession();
        MultiplayerRuntime.SetStatus("Preparing match: waiting for sync...");
        if (matchReady)
        {
            MultiplayerRuntime.MarkGameplayReady();
            MultiplayerRuntime.SetStatus("Connected. Match start");
        }
        NotifyPublicStateChanged();
    }

    private void OnDestroy()
    {
        OnPublicStateChanged = null;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    [Server]
    private void InitializeServerState()
    {
        if (serverInitialized && matchReady) return;

        MultiplayerCardCatalog.EnsureInitialized();

        EconomyManager economy = EconomyManager.Instance;
        CardDeckManager deckManager = CardDeckManager.Instance;

        if (economy != null)
        {
            initialMoney = economy.startingMoney;
        }

        if (deckManager != null)
        {
            handSize = Mathf.Max(1, deckManager.handSize);
            deckManager.EnableNetworkControlledMode();
        }

        playerByIndex.Clear();

        // Stable order helps deterministic fallback index assignment.
        NetworkConnectionToClient[] connections = NetworkServer.connections.Values
            .Where(c => c != null)
            .OrderBy(c => c.connectionId)
            .ToArray();

        foreach (NetworkConnectionToClient conn in connections)
        {
            if (conn == null || conn.identity == null) continue;
            MultiplayerPlayerIdentity identity = conn.identity.GetComponent<MultiplayerPlayerIdentity>();
            if (identity == null) continue;

            int index = identity.PlayerIndex;
            if (index < 0)
            {
                if (!playerByIndex.ContainsKey(0))
                {
                    index = 0;
                }
                else if (!playerByIndex.ContainsKey(1))
                {
                    index = 1;
                }

                if (index >= 0)
                {
                    identity.SetPlayerIndexServer(index);
                }
            }

            if (index < 0) continue;

            playerByIndex[index] = identity;

            if (!playerStates.ContainsKey(index))
            {
                playerStates[index] = BuildInitialState(index, deckManager);
            }
        }

        bool hasBothPlayers =
            playerByIndex.ContainsKey(0) &&
            playerByIndex.ContainsKey(1) &&
            playerStates.ContainsKey(0) &&
            playerStates.ContainsKey(1);

        if (!hasBothPlayers)
        {
            if (!serverInitialized)
            {
                matchReady = false;
            }
            Debug.LogWarning($"[TRACE][ServerInit] deferred: players ready={playerByIndex.Count} states={playerStates.Count}");
            return;
        }

        foreach (KeyValuePair<int, MultiplayerPlayerIdentity> pair in playerByIndex)
        {
            if (!playerStates.TryGetValue(pair.Key, out ServerPlayerState state)) continue;
            SetPublicStateFromServer(pair.Key, state);

            TrySendPrivateState(pair.Value, state);
            TrySendStatus(pair.Value, pair.Key == 0 ? "Host: control RIGHT side" : "Guest: control LEFT side");
        }

        TryBroadcastHandsToClients();

        endedThisRound[0] = false;
        endedThisRound[1] = false;

        serverInitialized = true;
        matchReady = true;
    }

    [Server]
    private ServerPlayerState BuildInitialState(int playerIndex, CardDeckManager deckManager)
    {
        ServerPlayerState state = new ServerPlayerState();
        state.money = initialMoney;

        List<string> baseDeck = new List<string>();
        if (deckManager != null && deckManager.deck.Count > 0)
        {
            foreach (CardData card in deckManager.deck)
            {
                if (card == null) continue;
                baseDeck.Add(MultiplayerCardCatalog.GetCardId(card));
            }
        }

        if (baseDeck.Count == 0)
        {
            CardData[] fallbackCards = Resources.LoadAll<CardData>("Card");
            foreach (CardData card in fallbackCards)
            {
                if (card == null) continue;
                baseDeck.Add(MultiplayerCardCatalog.GetCardId(card));
            }
        }

        System.Random random = new System.Random(Environment.TickCount + playerIndex * 1009);
        for (int i = baseDeck.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (baseDeck[i], baseDeck[j]) = (baseDeck[j], baseDeck[i]);
        }

        state.deck.AddRange(baseDeck);

        for (int i = 0; i < handSize; i++)
        {
            DrawCard(state);
        }

        return state;
    }

    [Server]
    public void ServerHandlePlaceCard(MultiplayerPlayerIdentity sender, string cardId, Vector3Int centerCell, int requestedHandIndex = -1)
    {
        EnsureServerStateReady();
        Debug.Log($"[TRACE][ServerPlace] recv cardId={cardId} cell=({centerCell.x},{centerCell.y},{centerCell.z}) sender={(sender == null ? "null" : sender.PlayerIndex.ToString())} matchReady={matchReady}");

        if (sender == null)
        {
            Debug.LogWarning("[TRACE][ServerPlace] rejected: sender null.");
            return;
        }

        if (!matchReady)
        {
            TrySendStatus(sender, "TRACE: rejected, match not ready");
            Debug.LogWarning("[TRACE][ServerPlace] rejected: match not ready.");
            return;
        }

        if (sender.PlayerIndex < 0)
        {
            Debug.LogWarning("[TRACE][ServerPlace] rejected: invalid sender player index.");
            return;
        }

        if (IsPlayerAlreadyEnded(sender.PlayerIndex))
        {
            TrySendStatus(sender, "TRACE: rejected, already ended this turn");
            Debug.Log("[TRACE][ServerPlace] rejected: already ended this round.");
            return;
        }

        if (!MultiplayerPlayAreaUtility.IsCellAllowedForPlayer(centerCell, sender.PlayerIndex))
        {
            Debug.LogWarning($"[TRACE][ServerPlace] area warning ignored: player={sender.PlayerIndex} cell=({centerCell.x},{centerCell.y},{centerCell.z})");
        }

        if (!playerStates.TryGetValue(sender.PlayerIndex, out ServerPlayerState state))
        {
            TrySendStatus(sender, "TRACE: rejected, server state missing");
            Debug.LogWarning("[TRACE][ServerPlace] rejected: player state missing.");
            return;
        }

        int handIndex = -1;
        string effectiveCardId = cardId;

        if (requestedHandIndex >= 0 && requestedHandIndex < state.hand.Count)
        {
            handIndex = requestedHandIndex;
            effectiveCardId = state.hand[handIndex];

            if (!string.Equals(cardId, effectiveCardId, StringComparison.Ordinal))
            {
                Debug.LogWarning($"[TRACE][ServerPlace] cardId mismatch resolved by slot. req={cardId} slot={requestedHandIndex} actual={effectiveCardId}");
            }
        }
        else
        {
            handIndex = state.hand.FindIndex(id => id == cardId);
            if (handIndex >= 0)
            {
                effectiveCardId = cardId;
            }
        }

        if (handIndex < 0)
        {
            TrySendStatus(sender, "TRACE: rejected, card not in hand");
            Debug.LogWarning($"[TRACE][ServerPlace] rejected: card not in hand. req={cardId} hand=[{string.Join(",", state.hand)}]");
            return;
        }

        CardData card = MultiplayerCardCatalog.Find(effectiveCardId);
        if (card == null)
        {
            TrySendStatus(sender, "TRACE: rejected, card sync failed");
            Debug.LogError("[TRACE][ServerPlace] rejected: card sync failed.");
            return;
        }

        if (card.cost > state.money)
        {
            TrySendStatus(sender, "TRACE: rejected, not enough money");
            Debug.Log("[TRACE][ServerPlace] rejected: not enough money.");
            return;
        }

        state.money -= card.cost;
        state.hand.RemoveAt(handIndex);
        DrawCard(state);

        if (card.effectType == CardEffectType.ManekinNeko)
        {
            state.money += Mathf.Max(0, card.bonusAmount);
        }

        SetPublicStateFromServer(sender.PlayerIndex, state);

        TrySendPlacement(effectiveCardId, centerCell, sender.PlayerIndex);
        TrySendPrivateState(sender, state);
        TryBroadcastHandsToClients();
        TrySendStatus(sender, "TRACE: placement accepted");
        Debug.Log("[TRACE][ServerPlace] accepted: placement applied.");
        MarkPlayerEnded(sender.PlayerIndex);
    }


    [Server]
    public void ServerHandleSkipTurn(MultiplayerPlayerIdentity sender)
    {
        EnsureServerStateReady();
        Debug.Log($"[TRACE][ServerSkip] recv sender={(sender == null ? "null" : sender.PlayerIndex.ToString())} matchReady={matchReady}");

        if (sender == null)
        {
            Debug.LogWarning("[TRACE][ServerSkip] rejected: sender null.");
            return;
        }

        if (!matchReady)
        {
            TrySendStatus(sender, "TRACE: rejected, match not ready");
            Debug.LogWarning("[TRACE][ServerSkip] rejected: match not ready.");
            return;
        }

        if (sender.PlayerIndex < 0)
        {
            Debug.LogWarning("[TRACE][ServerSkip] rejected: invalid sender player index.");
            return;
        }

        if (IsPlayerAlreadyEnded(sender.PlayerIndex))
        {
            TrySendStatus(sender, "TRACE: rejected, already ended this turn");
            Debug.Log("[TRACE][ServerSkip] rejected: already ended this round.");
            return;
        }

        TrySendStatus(sender, "TRACE: skip accepted");
        Debug.Log("[TRACE][ServerSkip] accepted.");
        MarkPlayerEnded(sender.PlayerIndex);
    }

    [Server]
    private bool IsPlayerAlreadyEnded(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < endedThisRound.Length && endedThisRound[playerIndex];
    }

    [Server]
    private void MarkPlayerEnded(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= endedThisRound.Length) return;

        endedThisRound[playerIndex] = true;

        if (!(endedThisRound[0] && endedThisRound[1]))
        {
            SetStatusForPlayer(playerIndex, "TRACE: action accepted, waiting opponent");

            int other = playerIndex == 0 ? 1 : 0;
            SetStatusForPlayer(other, "TRACE: opponent finished, choose action");
            Debug.Log($"[TRACE][TurnGate] endedThisRound=({endedThisRound[0]},{endedThisRound[1]}) waiting for other player.");
            return;
        }

        endedThisRound[0] = false;
        endedThisRound[1] = false;

        TryAdvanceTurnForClients();
        SetStatusForPlayer(0, "TRACE: both actions complete, next turn");
        SetStatusForPlayer(1, "TRACE: both actions complete, next turn");
        Debug.Log("[TRACE][TurnGate] both players ended. turn advanced.");
    }

    [Server]
    private void SetStatusForPlayer(int playerIndex, string message)
    {
        if (!playerByIndex.TryGetValue(playerIndex, out MultiplayerPlayerIdentity identity)) return;
        if (identity == null) return;

        TrySendStatus(identity, message);
    }

    [Server]
    private static void TrySendPrivateState(MultiplayerPlayerIdentity identity, ServerPlayerState state)
    {
        if (identity == null || state == null) return;

        try
        {
            NetworkConnectionToClient conn = identity.connectionToClient;
            if (conn == null)
            {
                Debug.LogWarning("[TRACE][ServerMSG] PrivateState skipped: target connection is null");
                return;
            }

            conn.Send(new PrivateStateMessage
            {
                handCsv = string.Join("|", state.hand),
                money = state.money
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TRACE][ServerMSG] PrivateState skipped: " + ex.Message);
        }
    }

    [Server]
    private static void TrySendStatus(MultiplayerPlayerIdentity identity, string message)
    {
        if (identity == null) return;

        try
        {
            NetworkConnectionToClient conn = identity.connectionToClient;
            if (conn == null)
            {
                Debug.LogWarning("[TRACE][ServerMSG] Status skipped: target connection is null");
                return;
            }

            conn.Send(new StatusMessage { message = message });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TRACE][ServerMSG] Status skipped: " + ex.Message);
        }
    }

    [Server]
    private void TrySendPlacement(string cardId, Vector3Int centerCell, int actorPlayerIndex)
    {
        try
        {
            if (!NetworkServer.active) return;

            NetworkServer.SendToAll(new PlacementAppliedMessage
            {
                cardId = cardId,
                x = centerCell.x,
                y = centerCell.y,
                z = centerCell.z,
                actorPlayerIndex = actorPlayerIndex
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TRACE][ServerMSG] Placement skipped: " + ex.Message);
        }
    }

    [Server]
    private void TryAdvanceTurnForClients()
    {
        try
        {
            if (!NetworkServer.active) return;

            NetworkServer.SendToAll(new AdvanceTurnMessage());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TRACE][ServerMSG] AdvanceTurn skipped: " + ex.Message);
        }
    }

    [Server]
    private static void DrawCard(ServerPlayerState state)
    {
        if (state.deck.Count <= 0) return;

        string top = state.deck[0];
        state.deck.RemoveAt(0);
        state.hand.Add(top);
    }

    [Server]
    private void EnsureServerStateReady()
    {
        if (matchReady) return;
        InitializeServerState();
    }

    [Mirror.ClientRpc]
    private void RpcApplyPlacement(string cardId, int x, int y, int z, int actorPlayerIndex)
    {
        CardData card = MultiplayerCardCatalog.Find(cardId);
        if (card == null)
        {
            MultiplayerRuntime.SetStatus("Card sync error: " + cardId);
            return;
        }

        if (CardPlacementController.Instance == null)
        {
            MultiplayerRuntime.SetStatus("Placement controller not found");
            return;
        }

        bool clearSelection = MultiplayerRuntime.LocalPlayerIndex == actorPlayerIndex;
        CardPlacementController.Instance.ApplyPlacementFromNetwork(card, new Vector3Int(x, y, z), clearSelection);
    }

    [Mirror.ClientRpc]
    private void RpcAdvanceTurnOnly()
    {
        TurnManager.Instance?.AdvanceTurn();
    }

    private void OnMatchReadyChanged(bool _, bool isReady)
    {
        if (!isReady) return;
        MultiplayerRuntime.MarkGameplayReady();
        MultiplayerRuntime.SetStatus("Connected. Match start");
    }

    private void OnPublicValueChanged(int _, int __)
    {
        NotifyPublicStateChanged();
    }

    [Server]
    private void SetPublicStateFromServer(int playerIndex, ServerPlayerState state)
    {
        if (state == null) return;

        if (playerIndex == 0)
        {
            player0Money = state.money;
            player0HandCount = state.hand.Count;
            return;
        }

        if (playerIndex == 1)
        {
            player1Money = state.money;
            player1HandCount = state.hand.Count;
        }
    }

    private static void NotifyPublicStateChanged()
    {
        OnPublicStateChanged?.Invoke();
    }

    [Server]
    private void TryBroadcastHandsToClients()
    {
        try
        {
            if (!NetworkServer.active)
            {
                return;
            }

            string player0HandCsv = string.Empty;
            string player1HandCsv = string.Empty;

            if (playerStates.TryGetValue(0, out ServerPlayerState state0) && state0 != null)
            {
                player0HandCsv = string.Join("|", state0.hand);
            }

            if (playerStates.TryGetValue(1, out ServerPlayerState state1) && state1 != null)
            {
                player1HandCsv = string.Join("|", state1.hand);
            }

            NetworkServer.SendToAll(new HandsSyncMessage
            {
                player0HandCsv = player0HandCsv,
                player1HandCsv = player1HandCsv
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TRACE][ServerMSG] HandsSync skipped: " + ex.Message);
        }
    }

    [Mirror.ClientRpc]
    private void RpcSyncHands(string player0HandCsv, string player1HandCsv)
    {
        if (CardDeckManager.Instance == null) return;

        List<CardData> player0Cards = ParseCardList(player0HandCsv);
        List<CardData> player1Cards = ParseCardList(player1HandCsv);
        CardDeckManager.Instance.SetBothHandsFromNetwork(player0Cards, player1Cards);
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

    public static void ApplyLocalPrivateState(string handCsv, int money)
    {
        List<CardData> cards = ParseCardList(handCsv);

        if (CardDeckManager.Instance != null)
        {
            CardDeckManager.Instance.EnableNetworkControlledMode();
            CardDeckManager.Instance.SetHandFromNetwork(cards);
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.SetMoney(money);
        }

        MultiplayerRuntime.MarkGameplayReady();
    }
}
