using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class MultiplayerMatchCoordinator : NetworkBehaviour
{
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

        GameObject go = new GameObject("MultiplayerMatchCoordinator");
        NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
        MultiplayerMatchCoordinator coordinator = go.AddComponent<MultiplayerMatchCoordinator>();
        NetworkServer.Spawn(go);

        if (identity == null || coordinator == null)
        {
            Debug.LogError("MultiplayerMatchCoordinator: spawn failed.");
        }
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

    public override void OnStartClient()
    {
        base.OnStartClient();
        MultiplayerRuntime.EnterMultiplayerSession();
        MultiplayerRuntime.SetStatus("対戦準備中: 相手の同期を待機しています...");
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
        if (serverInitialized) return;

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

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn.identity == null) continue;
            MultiplayerPlayerIdentity identity = conn.identity.GetComponent<MultiplayerPlayerIdentity>();
            if (identity == null) continue;

            playerByIndex[identity.PlayerIndex] = identity;
            playerStates[identity.PlayerIndex] = BuildInitialState(identity.PlayerIndex, deckManager);
        }

        foreach (KeyValuePair<int, MultiplayerPlayerIdentity> pair in playerByIndex)
        {
            if (!playerStates.TryGetValue(pair.Key, out ServerPlayerState state)) continue;
            SetPublicStateFromServer(pair.Key, state);
            pair.Value.RpcApplyPrivateState(string.Join("|", state.hand), state.money);
            pair.Value.RpcSetStatus(pair.Key == 0 ? "Player1: 左側を操作してください" : "Player2: 右側を操作してください");
        }

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
    public void ServerHandlePlaceCard(MultiplayerPlayerIdentity sender, string cardId, Vector3Int centerCell)
    {
        if (!matchReady || sender == null) return;
        if (sender.PlayerIndex < 0) return;

        if (IsPlayerAlreadyEnded(sender.PlayerIndex))
        {
            sender.RpcSetStatus("あなたはこのターンの行動を完了しています。相手を待ってください。");
            return;
        }

        if (!MultiplayerPlayAreaUtility.IsCellAllowedForPlayer(centerCell, sender.PlayerIndex))
        {
            sender.RpcSetStatus("操作エリア外には配置できません");
            return;
        }

        if (!playerStates.TryGetValue(sender.PlayerIndex, out ServerPlayerState state)) return;

        int handIndex = state.hand.FindIndex(id => id == cardId);
        if (handIndex < 0)
        {
            sender.RpcSetStatus("そのカードは手札にありません");
            return;
        }

        CardData card = MultiplayerCardCatalog.Find(cardId);
        if (card == null)
        {
            sender.RpcSetStatus("カードデータの同期に失敗しました");
            return;
        }

        if (card.cost > state.money)
        {
            sender.RpcSetStatus("所持金が足りません");
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

        RpcApplyPlacement(cardId, centerCell.x, centerCell.y, centerCell.z, sender.PlayerIndex);
        sender.RpcApplyPrivateState(string.Join("|", state.hand), state.money);
        MarkPlayerEnded(sender.PlayerIndex);
    }

    [Server]
    public void ServerHandleSkipTurn(MultiplayerPlayerIdentity sender)
    {
        if (!matchReady || sender == null) return;
        if (sender.PlayerIndex < 0) return;

        if (IsPlayerAlreadyEnded(sender.PlayerIndex))
        {
            sender.RpcSetStatus("あなたはこのターンの行動を完了しています。相手を待ってください。");
            return;
        }

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
            SetStatusForPlayer(playerIndex, "行動完了。相手の行動を待っています...");

            int other = playerIndex == 0 ? 1 : 0;
            SetStatusForPlayer(other, "相手が行動完了しました。あなたの行動を選択してください。");
            return;
        }

        endedThisRound[0] = false;
        endedThisRound[1] = false;

        RpcAdvanceTurnOnly();
        SetStatusForPlayer(0, "両プレイヤーの行動が完了。次ターンへ進みます。");
        SetStatusForPlayer(1, "両プレイヤーの行動が完了。次ターンへ進みます。");
    }

    [Server]
    private void SetStatusForPlayer(int playerIndex, string message)
    {
        if (!playerByIndex.TryGetValue(playerIndex, out MultiplayerPlayerIdentity identity)) return;
        if (identity == null) return;

        identity.RpcSetStatus(message);
    }

    [Server]
    private static void DrawCard(ServerPlayerState state)
    {
        if (state.deck.Count <= 0) return;

        string top = state.deck[0];
        state.deck.RemoveAt(0);
        state.hand.Add(top);
    }

    [Mirror.ClientRpc]
    private void RpcApplyPlacement(string cardId, int x, int y, int z, int actorPlayerIndex)
    {
        CardData card = MultiplayerCardCatalog.Find(cardId);
        if (card == null)
        {
            MultiplayerRuntime.SetStatus("カード同期エラー: " + cardId);
            return;
        }

        if (CardPlacementController.Instance == null)
        {
            MultiplayerRuntime.SetStatus("配置コントローラーが見つかりません");
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
        MultiplayerRuntime.SetStatus("接続完了: ゲーム開始");
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

    public static void ApplyLocalPrivateState(string handCsv, int money)
    {
        List<CardData> cards = new List<CardData>();
        if (!string.IsNullOrEmpty(handCsv))
        {
            string[] ids = handCsv.Split('|');
            foreach (string id in ids)
            {
                CardData card = MultiplayerCardCatalog.Find(id);
                if (card != null) cards.Add(card);
            }
        }

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
