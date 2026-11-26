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

    [SyncVar] private bool timeReady0;
    [SyncVar] private bool timeReady1;
    [SyncVar] private bool timeTurnActive;
    private Coroutine timeTurnCoroutine;

    public bool TimeReady0 => timeReady0;
    public bool TimeReady1 => timeReady1;
    public bool TimeTurnActive => timeTurnActive;

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
    private readonly bool[] reloadedThisRound = new bool[2];

    private int initialMoney = 100;
    private int handSize = 4;
    private bool serverInitialized;

    private static bool hasPendingLocalPrivateState;
    private static string pendingHandCsv = string.Empty;
    private static int pendingMoney;

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
        // NetworkIdentity caches NetworkBehaviours in Awake; add it after the
        // coordinator so its SyncVars and lifecycle callbacks are registered.
        go.AddComponent<MultiplayerMatchCoordinator>();
        go.AddComponent<NetworkIdentity>();
        return go;
    }

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        SubscribeToWaveCompletion();
        InitializeServerState();
    }

    public override void OnStopServer()
    {
        StopTimeTurn();
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveCompleted.RemoveListener(OnServerWaveCompleted);
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[TRACE][PublicMoney] coordinator client started p0={player0Money} p1={player1Money} ready={matchReady}");
        MultiplayerRuntime.EnterMultiplayerSession();
        MultiplayerRuntime.SetStatus("Preparing match: waiting for sync...");
        if (matchReady)
        {
            MultiplayerRuntime.MarkGameplayReady();
            MultiplayerRuntime.SetStatus("Connected. Match start");
        }
        TrySyncLocalEconomyFromPublicState();
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

        SubscribeToWaveCompletion();

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
                // Do not invent a second player-index mapping here. The network
                // manager assigns it before spawning; wait for that authoritative value.
                Debug.LogWarning($"[TRACE][ServerInit] waiting for player index sync on connection {conn.connectionId}");
                continue;
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
            Debug.Log($"[TRACE][ServerInit] player={pair.Key} initialMoney={state.money} handCount={state.hand.Count}");

            TrySendPrivateState(pair.Value, state);
            TrySendStatus(pair.Value, pair.Key == 0 ? "Host: control RIGHT side" : "Guest: control LEFT side");
        }

        TryBroadcastHandsToClients();

        endedThisRound[0] = false;
        endedThisRound[1] = false;
        reloadedThisRound[0] = false;
        reloadedThisRound[1] = false;
        TryBroadcastTurnCoverState(false, false);

        serverInitialized = true;
        matchReady = true;
        Debug.Log($"[TRACE][ServerInit] ready p0Money={player0Money} p1Money={player1Money}");
    }

    [Server]
    private void SubscribeToWaveCompletion()
    {
        if (WaveManager.Instance == null) return;
        WaveManager.Instance.OnWaveCompleted.RemoveListener(OnServerWaveCompleted);
        WaveManager.Instance.OnWaveCompleted.AddListener(OnServerWaveCompleted);
    }

    [Server]
    private void OnServerWaveCompleted(int completedWave)
    {
        if (completedWave <= 0 || completedWave % 5 != 0) return;

        CardDeckManager deckManager = CardDeckManager.Instance;
        foreach (var pair in playerStates)
        {
            if (pair.Value == null) continue;
            int currentMoney = pair.Value.money;
            ServerPlayerState refreshed = BuildInitialState(pair.Key, deckManager);

            pair.Value.deck.Clear();
            pair.Value.deck.AddRange(refreshed.deck);
            pair.Value.hand.Clear();
            pair.Value.hand.AddRange(refreshed.hand);
            pair.Value.money = currentMoney;

            SetPublicStateFromServer(pair.Key, pair.Value);
            if (playerByIndex.TryGetValue(pair.Key, out MultiplayerPlayerIdentity identity))
                TrySendPrivateState(identity, pair.Value);
        }

        reloadedThisRound[0] = false;
        reloadedThisRound[1] = false;
        TryBroadcastHandsToClients();
        Debug.Log($"[TRACE][ServerDeck] restored both player decks after Wave{completedWave}");
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

        if (FieldGridConfig.Instance != null && !FieldGridConfig.Instance.IsWalkable(centerCell))
        {
            TrySendStatus(sender, "TRACE: rejected, invalid cell");
            Debug.LogWarning($"[TRACE][ServerPlace] rejected: invalid cell (not walkable). player={sender.PlayerIndex} cell=({centerCell.x},{centerCell.y},{centerCell.z})");
            return;
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
            TrySendStatus(sender, $"TRACE: rejected, not enough money (have={state.money}, need={card.cost}, card={effectiveCardId})");
            Debug.LogWarning($"[TRACE][ServerPlace] rejected: not enough money. player={sender.PlayerIndex} have={state.money} need={card.cost} requested={cardId} effective={effectiveCardId} handIndex={handIndex}");
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
    public void ServerToggleTimeTurnReady(MultiplayerPlayerIdentity sender)
    {
        if (sender == null || !matchReady || sender.PlayerIndex < 0 || sender.PlayerIndex > 1) return;
        if (timeTurnActive)
        {
            StopTimeTurn();
            return;
        }

        if (sender.PlayerIndex == 0) timeReady0 = !timeReady0;
        else timeReady1 = !timeReady1;

        if (timeReady0 && timeReady1)
        {
            timeTurnActive = true;
            timeTurnCoroutine = StartCoroutine(TimeTurnLoop());
        }
    }

    [Server]
    public void ServerResetTimeTurn()
    {
        StopTimeTurn();
    }

    [Server]
    public void ServerHandleAnimalIntimidation(MultiplayerPlayerIdentity sender, int animalId, int cost)
    {
        if (sender == null || !matchReady || animalId <= 0 || IsPlayerAlreadyEnded(sender.PlayerIndex)) return;
        if (!playerStates.TryGetValue(sender.PlayerIndex, out var state) || state == null)
        {
            TrySendStatus(sender, "Intimidation failed: player state missing");
            return;
        }
        cost = Mathf.Max(0, cost);
        if (state.money < cost)
        {
            TrySendStatus(sender, $"Intimidation failed: not enough money (need={cost}, have={state.money})");
            return;
        }
        AnimalController target = null;
        foreach (var animal in FindObjectsByType<AnimalController>(FindObjectsSortMode.None))
            if (animal != null && animal.NetworkAnimalId == animalId) { target = animal; break; }

        if (target == null || !target.TryGetRetreatCell(out Vector3Int retreatCell) || !target.TryMoveToCell(retreatCell))
        {
            TrySendStatus(sender, "Intimidation failed: animal cannot retreat");
            return;
        }

        state.money -= cost;
        SetPublicStateFromServer(sender.PlayerIndex, state);
        TrySendPrivateState(sender, state);
        target.ApplyIntimidationForNextTurn();
        MultiplayerNetworkManager.BroadcastAuthoritativeAnimalState();
        ServerHandleSkipTurn(sender);
    }

    [Server]
    private System.Collections.IEnumerator TimeTurnLoop()
    {
        while (timeTurnActive)
        {
            float interval = TurnManager.Instance != null ? TurnManager.Instance.TimeTurnInterval : 1f;
            yield return new WaitForSeconds(interval);
            if (!timeTurnActive) yield break;
            endedThisRound[0] = false;
            endedThisRound[1] = false;
            MarkPlayerEnded(0, true);
            MarkPlayerEnded(1, true);
        }
    }

    [Server]
    private void StopTimeTurn()
    {
        timeTurnActive = false;
        timeReady0 = false;
        timeReady1 = false;
        if (timeTurnCoroutine != null)
        {
            StopCoroutine(timeTurnCoroutine);
            timeTurnCoroutine = null;
        }
        endedThisRound[0] = false;
        endedThisRound[1] = false;
        TryBroadcastTurnCoverState(false, false);
    }

    [Server]
    public void ServerHandleReloadHand(MultiplayerPlayerIdentity sender)
    {
        EnsureServerStateReady();
        int playerIndex = sender != null ? sender.PlayerIndex : -1;
        Debug.Log($"[TRACE][ServerReload] recv sender={playerIndex} matchReady={matchReady}");

        if (sender == null || !matchReady || playerIndex < 0 || playerIndex >= reloadedThisRound.Length)
        {
            if (sender != null) TrySendStatus(sender, "Reload rejected: player or match is not ready");
            Debug.LogWarning("[TRACE][ServerReload] rejected: invalid sender or match not ready.");
            return;
        }

        if (IsPlayerAlreadyEnded(playerIndex))
        {
            TrySendStatus(sender, "Reload rejected: action already submitted this round");
            Debug.Log("[TRACE][ServerReload] rejected: player already ended this round.");
            return;
        }

        if (reloadedThisRound[playerIndex])
        {
            TrySendStatus(sender, "Reload rejected: already reloaded this round");
            Debug.Log("[TRACE][ServerReload] rejected: already reloaded this round.");
            return;
        }

        if (!playerStates.TryGetValue(playerIndex, out ServerPlayerState state) || state == null)
        {
            TrySendStatus(sender, "Reload rejected: player state missing");
            Debug.LogWarning("[TRACE][ServerReload] rejected: player state missing.");
            return;
        }

        int reloadCost = CardDeckManager.Instance != null
            ? Mathf.Max(0, CardDeckManager.Instance.reloadCost)
            : 50;
        if (state.money < reloadCost)
        {
            TrySendStatus(sender, $"Reload rejected: not enough money (need={reloadCost}, have={state.money})");
            Debug.LogWarning($"[TRACE][ServerReload] rejected: not enough money player={playerIndex} have={state.money} need={reloadCost}");
            return;
        }

        if (state.hand.Count == 0 && state.deck.Count == 0)
        {
            TrySendStatus(sender, "Reload rejected: no cards remain");
            Debug.LogWarning($"[TRACE][ServerReload] rejected: no cards remain for player={playerIndex}");
            return;
        }

        state.deck.AddRange(state.hand);
        state.hand.Clear();
        ShuffleDeck(state.deck, playerIndex);
        for (int i = 0; i < handSize && state.deck.Count > 0; i++)
        {
            DrawCard(state);
        }

        state.money -= reloadCost;
        reloadedThisRound[playerIndex] = true;
        SetPublicStateFromServer(playerIndex, state);
        TrySendPrivateState(sender, state);
        TryBroadcastHandsToClients();
        TrySendStatus(sender, $"Hand reloaded (cost={reloadCost})");
        Debug.Log($"[TRACE][ServerReload] accepted player={playerIndex} handCount={state.hand.Count} cost={reloadCost} money={state.money}");
    }

    [Server]
    private static void ShuffleDeck(List<string> deck, int playerIndex)
    {
        System.Random random = new System.Random(Environment.TickCount + playerIndex * 1009);
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    [Server]
    private bool IsPlayerAlreadyEnded(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < endedThisRound.Length && endedThisRound[playerIndex];
    }

    [Server]
    private void MarkPlayerEnded(int playerIndex, bool fromTimeTurn = false)
    {
        if (playerIndex < 0 || playerIndex >= endedThisRound.Length) return;

        // During time mode, ignore player-triggered round advancement while
        // still allowing the server timer to submit both players' turn ends.
        if (timeTurnActive && !fromTimeTurn) return;

        endedThisRound[playerIndex] = true;

        if (!(endedThisRound[0] && endedThisRound[1]))
        {
            TryBroadcastTurnCoverState(endedThisRound[0], endedThisRound[1]);
            SetStatusForPlayer(playerIndex, "TRACE: action accepted, waiting opponent");

            int other = playerIndex == 0 ? 1 : 0;
            SetStatusForPlayer(other, "TRACE: opponent finished, choose action");
            Debug.Log($"[TRACE][TurnGate] endedThisRound=({endedThisRound[0]},{endedThisRound[1]}) waiting for other player.");
            return;
        }

        endedThisRound[0] = false;
        endedThisRound[1] = false;
        reloadedThisRound[0] = false;
        reloadedThisRound[1] = false;

        TryBroadcastTurnCoverState(false, false);

        ApplyTurnIncomeOnServerAndSync();
        TryAdvanceTurnForClients();
        SetStatusForPlayer(0, "TRACE: both actions complete, next turn");
        SetStatusForPlayer(1, "TRACE: both actions complete, next turn");
        Debug.Log("[TRACE][TurnGate] both players ended. turn advanced.");
    }

    [Server]
    private void ApplyTurnIncomeOnServerAndSync()
    {
        int income = 0;
        if (EconomyManager.Instance != null)
        {
            income = Mathf.Max(0, EconomyManager.Instance.incomePerTurn);
        }

        if (income <= 0)
        {
            foreach (KeyValuePair<int, MultiplayerPlayerIdentity> pair in playerByIndex)
            {
                if (pair.Value == null) continue;
                if (!playerStates.TryGetValue(pair.Key, out ServerPlayerState state) || state == null) continue;

                SetPublicStateFromServer(pair.Key, state);
                TrySendPrivateState(pair.Value, state);
            }

            return;
        }

        foreach (KeyValuePair<int, MultiplayerPlayerIdentity> pair in playerByIndex)
        {
            if (pair.Value == null) continue;
            if (!playerStates.TryGetValue(pair.Key, out ServerPlayerState state) || state == null) continue;

            state.money += income;
            SetPublicStateFromServer(pair.Key, state);
            TrySendPrivateState(pair.Value, state);
        }
    }

    [Server]
    private MultiplayerPlayerIdentity ResolveServerIdentityForPlayerIndex(int playerIndex)
    {
        if (playerIndex < 0) return null;

        if (playerByIndex.TryGetValue(playerIndex, out MultiplayerPlayerIdentity cached) && cached != null)
        {
            try
            {
                if (cached.netIdentity != null)
                {
                    return cached;
                }
            }
            catch
            {
            }
        }

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn.identity == null) continue;

            MultiplayerPlayerIdentity identity = conn.identity.GetComponent<MultiplayerPlayerIdentity>();
            if (identity == null) continue;
            if (identity.PlayerIndex != playerIndex) continue;

            playerByIndex[playerIndex] = identity;
            return identity;
        }

        return null;
    }

    [Server]
    private void SetStatusForPlayer(int playerIndex, string message)
    {
        MultiplayerPlayerIdentity identity = ResolveServerIdentityForPlayerIndex(playerIndex);
        if (identity == null) return;

        TrySendStatus(identity, message);
    }

    [Server]
    private static NetworkConnectionToClient ResolveConnectionForPlayer(MultiplayerPlayerIdentity identity)
    {
        if (identity == null) return null;

        try
        {
            if (identity.connectionToClient != null) return identity.connectionToClient;
        }
        catch
        {
        }

        try
        {
            if (identity.netIdentity != null && identity.netIdentity.connectionToClient != null)
            {
                return identity.netIdentity.connectionToClient;
            }
        }
        catch
        {
        }

        try
        {
            foreach (var pair in NetworkServer.connections)
            {
                if (pair.Value == null || pair.Value.identity == null) continue;
                if (pair.Value.identity.netId == identity.netId)
                {
                    return pair.Value;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    [Server]
    private static void TrySendPrivateState(MultiplayerPlayerIdentity identity, ServerPlayerState state)
    {
        if (identity == null || state == null) return;

        NetworkConnectionToClient conn = ResolveConnectionForPlayer(identity);
        if (conn == null)
        {
            Debug.LogWarning("[TRACE][ServerMSG] PrivateState skipped: target connection is null");
            return;
        }

        try
        {
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

        NetworkConnectionToClient conn = ResolveConnectionForPlayer(identity);
        if (conn == null)
        {
            Debug.LogWarning("[TRACE][ServerMSG] Status skipped: target connection is null");
            return;
        }

        try
        {
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
    private void TryBroadcastTurnCoverState(bool cover0Visible, bool cover1Visible)
    {
        try
        {
            if (!NetworkServer.active) return;

            NetworkServer.SendToAll(new TurnCoverStateMessage
            {
                cover0Visible = cover0Visible,
                cover1Visible = cover1Visible
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TRACE][ServerMSG] TurnCoverState skipped: " + ex.Message);
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

    [Server]
    public void ServerTryInitializeAndSyncAll()
    {
        EnsureServerStateReady();
        if (!matchReady) return;

        foreach (KeyValuePair<int, MultiplayerPlayerIdentity> pair in playerByIndex)
        {
            if (pair.Value == null) continue;
            if (!playerStates.TryGetValue(pair.Key, out ServerPlayerState state)) continue;

            SetPublicStateFromServer(pair.Key, state);
            TrySendPrivateState(pair.Value, state);
            TrySendStatus(pair.Value, pair.Key == 0 ? "Host: control RIGHT side" : "Guest: control LEFT side");
        }

        TryBroadcastHandsToClients();
    }

    [Server]
    public void ServerSyncStateToConnection(NetworkConnectionToClient conn)
    {
        EnsureServerStateReady();
        if (!matchReady) return;
        if (conn == null) return;

        MultiplayerPlayerIdentity identity = null;
        if (conn.identity != null)
        {
            identity = conn.identity.GetComponent<MultiplayerPlayerIdentity>();
        }

        if (identity == null)
        {
            foreach (NetworkIdentity ni in NetworkServer.spawned.Values)
            {
                if (ni == null) continue;
                if (ni.connectionToClient != conn) continue;
                identity = ni.GetComponent<MultiplayerPlayerIdentity>();
                if (identity != null) break;
            }
        }

        if (identity == null) return;
        if (!playerStates.TryGetValue(identity.PlayerIndex, out ServerPlayerState state)) return;

        SetPublicStateFromServer(identity.PlayerIndex, state);
        TrySendPrivateState(identity, state);
        TrySendStatus(identity, identity.PlayerIndex == 0 ? "Host: control RIGHT side" : "Guest: control LEFT side");
        TryBroadcastHandsToClients();
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

        bool clearSelection = MultiplayerRuntime.GetEffectiveLocalPlayerIndex() == actorPlayerIndex;
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
        TrySyncLocalEconomyFromPublicState();
    }

    private void OnPublicValueChanged(int _, int __)
    {
        TrySyncLocalEconomyFromPublicState();
        NotifyPublicStateChanged();
    }

    private void TrySyncLocalEconomyFromPublicState()
    {
        if (!MultiplayerRuntime.IsMultiplayerSession) return;
        if (EconomyManager.Instance == null) return;

        int localIndex = MultiplayerRuntime.GetEffectiveLocalPlayerIndex();
        if (localIndex != 0 && localIndex != 1) return;

        int expectedMoney = localIndex == 0 ? player0Money : player1Money;
        if (EconomyManager.Instance.CurrentMoney == expectedMoney) return;

        Debug.Log($"[TRACE][MoneySync] localPlayerIndex={localIndex} serverMoney={expectedMoney} current={EconomyManager.Instance.CurrentMoney}");
        EconomyManager.Instance.SetMoney(expectedMoney);
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
        Debug.Log($"[TRACE][AuthoritativeState] ApplyLocalPrivateState localIndex={MultiplayerRuntime.GetEffectiveLocalPlayerIndex()} handCsv={handCsv ?? "<null>"} money={money}");

        if (CardDeckManager.Instance == null || EconomyManager.Instance == null)
        {
            hasPendingLocalPrivateState = true;
            pendingHandCsv = handCsv ?? string.Empty;
            pendingMoney = money;
            return;
        }

        List<CardData> cards = ParseCardList(handCsv);
        Debug.Log($"[TRACE][AuthoritativeState] parsedCards={cards.Count} targetMoney={money} localIndex={MultiplayerRuntime.GetEffectiveLocalPlayerIndex()}");

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

    public static void TryApplyPendingLocalPrivateState()
    {
        if (!hasPendingLocalPrivateState) return;
        if (CardDeckManager.Instance == null || EconomyManager.Instance == null) return;

        string handCsv = pendingHandCsv;
        int money = pendingMoney;

        hasPendingLocalPrivateState = false;
        pendingHandCsv = string.Empty;
        pendingMoney = 0;

        ApplyLocalPrivateState(handCsv, money);
    }
}
