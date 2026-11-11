using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using Mirror;

/// <summary>
/// カードの山札を管理するシステム
/// </summary>
public class CardDeckManager : MonoBehaviour
{
    public static CardDeckManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    
    [Header("山札")]
    public List<CardData> deck = new List<CardData>();

    [Header("手札")]
    public List<CardData> hand = new List<CardData>();

    [Header("手札UI")]
    public List<CardSlotUI> cardSlots = new List<CardSlotUI>();

    [Header("設定")]
    public int handSize = 4;

    [Header("リロード設定")]
    public int reloadCost = 50;
    
    // 最後にリロードしたターンの記憶
    private int lastReloadTurn = -1;

    // ゲーム開始時の山札を保存
    private List<CardData> initialDeck = new List<CardData>();

    private bool networkControlledMode;
    private bool resolvedLocalSideSlots;
    private int lastRenderedPlayerIndex = -99;
    private readonly List<CardSlotUI> leftSideSlots = new List<CardSlotUI>();
    private readonly List<CardSlotUI> rightSideSlots = new List<CardSlotUI>();
    private List<CardData> latestHostHand = new List<CardData>();
    private List<CardData> latestGuestHand = new List<CardData>();

    private void Start()
    {
        if (networkControlledMode)
        {
            UpdateHandUI();
            return;
        }

        // 手札を引く前の山札を保存
        initialDeck = new List<CardData>(deck);

        DrawInitialHand();
        UpdateHandUI();

        // Wave終了時の招き猫ボーナスを購読
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveCompleted.AddListener(OnWaveCompleted);
        else
            Debug.LogWarning("CardDeckManager: WaveManager が見つかりません。招き猫のWave終了ボーナスが機能しません。");
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveCompleted.RemoveListener(OnWaveCompleted);
    }

    private void Update()
    {
        if (!MultiplayerRuntime.IsMultiplayerSession) return;
        if (!networkControlledMode) return;

        if (!resolvedLocalSideSlots)
        {
            ResolveSlotsForLocalPlayerSide();
        }

        int currentPlayerIndex = MultiplayerRuntime.LocalPlayerIndex;
        if (currentPlayerIndex != lastRenderedPlayerIndex)
        {
            lastRenderedPlayerIndex = currentPlayerIndex;
            RenderMultiplayerHands();
        }
    }

    /// <summary>
    /// Wave終了時に呼ばれる。手札に招き猫が残っていれば所持金を1.5倍にする。
    /// 複数枚ある場合は1枚ごとに1.5倍を適用する。
    /// </summary>
    private void OnWaveCompleted(int completedWave)
    {
        if (networkControlledMode) return;
        if (EconomyManager.Instance == null) return;

        int manekinNekoCount = 0;
        foreach (var card in hand)
        {
            if (card != null && card.effectType == CardEffectType.ManekinNeko)
                manekinNekoCount++;
        }

        for (int i = 0; i < manekinNekoCount; i++)
        {
            int current = EconomyManager.Instance.CurrentMoney;
            int bonus = Mathf.RoundToInt(current * 0.5f);
            EconomyManager.Instance.AddMoney(bonus);
            Debug.Log($"[招き猫] Wave{completedWave} 終了ボーナス: {current} → {EconomyManager.Instance.CurrentMoney} (+{bonus})");
        }

        // 5 waveごとに山札と手札を初期化
        if (completedWave % 5 == 0)
        {
            ResetDeckAndHand();
        }
    }

    /// <summary>
    /// 最初に手札を4枚引く。
    /// </summary>
    public void DrawInitialHand()
    {
        hand.Clear();

        for (int i = 0; i < handSize; i++)
        {
            DrawRandomCard();
        }
    }

    /// <summary>
    /// 山札からランダムに1枚引く。
    /// </summary>
    private CardData DrawRandomCard()
    {
        if (deck.Count == 0)
        {
            Debug.LogWarning("山札が空です。");
            return null;
        }

        int index = Random.Range(0, deck.Count);

        CardData card = deck[index];

        hand.Add(card);
        deck.RemoveAt(index);

        return card;
    }

    /// <summary>
    /// カードを1枚使ったときに呼び出す。
    /// 使用したカードを手札から削除し、
    /// 山札から1枚引いて補充する。
    /// </summary>
    public void UseCard(CardData card)
    {
        if (networkControlledMode) return;
        if (card == null) return;

        // 手札からカードを削除
        if (!hand.Remove(card))
        {
            Debug.LogWarning("使用しようとしたカードが手札にありません。");
            return;
        }

        // 山札から1枚補充
        DrawRandomCard();

        // UIを更新
        UpdateHandUI();
    }

    /// <summary>
    /// 手札をすべてリロードする。
    /// 1ターンにつき1回、無料で行える
    /// </summary>
    public void ReloadHand()
    {
        if (networkControlledMode) return;
        // ターン管理の確認
        if (TurnManager.Instance == null)
        {
            Debug.LogWarning("TurnManagerが見つからないため、リロードできません。");
            return;
        }
        
        int currentTurn = TurnManager.Instance.CurrentTurn;
        
        // 同じターンにすでにリロードしている場合
        if (lastReloadTurn == currentTurn)
        {
            Debug.Log("このターンはすでにリロードしています。");
            return;
        }
        
        // 山札が空の場合は引き直せない
        if (deck.Count == 0)
        {
            Debug.Log("山札が空のため、リロードできません。");
            return;
        }
        
        // 現在の手札を山札に戻す
        foreach (CardData card in hand)
        {
            if (card != null)
            {
                deck.Add(card);
            }
        }
        
        hand.Clear();
        
        // 手札を引き直す
        for (int i = 0; i < handSize && deck.Count > 0; i++)
        {
            DrawRandomCard();
        }
        
        // リロードしたターンを記録
        lastReloadTurn = currentTurn;
        
        // UIを更新
        UpdateHandUI();
        
        Debug.Log($"手札をリロードしました。");
    }


    /// <summary>
    /// 手札をUIに表示する。
    /// </summary>
    private void UpdateHandUI()
    {
        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (i < hand.Count)
            {
                cardSlots[i].gameObject.SetActive(true);
                cardSlots[i].SetCard(hand[i]);
            }
            else
            {
                cardSlots[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 山札と手札をゲーム開始時の状態に戻す。
    /// 5 waveごとに呼び出す。
    /// </summary>
    public void ResetDeckAndHand()
    {
        if (networkControlledMode) return;
        // 手札を空にする
        hand.Clear();

        // 山札を初期状態に戻す
        deck.Clear();
        deck.AddRange(initialDeck);

        // リロード制限もリセット
        lastReloadTurn = -1;

        // 初期状態の山札から手札を引き直す
        DrawInitialHand();

        // 手札の表示を更新
        UpdateHandUI();

        Debug.Log("5 wave経過：山札と手札を初期状態に戻しました。");
    }

    public void EnableNetworkControlledMode()
    {
        networkControlledMode = true;
    }

    public void SetHandFromNetwork(List<CardData> newHand)
    {
        networkControlledMode = true;

        ResolveSlotsForLocalPlayerSide();
        int localIndex = GetEffectiveLocalPlayerIndex();

        List<CardData> source = newHand ?? new List<CardData>();

        if (localIndex == 0)
        {
            latestHostHand = new List<CardData>(source);
        }
        else if (localIndex == 1)
        {
            latestGuestHand = new List<CardData>(source);
        }

        hand.Clear();
        hand.AddRange(source);

        RenderMultiplayerHands();
    }

    public void SetBothHandsFromNetwork(List<CardData> hostHand, List<CardData> guestHand)
    {
        networkControlledMode = true;
        ResolveSlotsForLocalPlayerSide();
        int localIndex = GetEffectiveLocalPlayerIndex();

        latestHostHand = hostHand != null ? new List<CardData>(hostHand) : new List<CardData>();
        latestGuestHand = guestHand != null ? new List<CardData>(guestHand) : new List<CardData>();

        hand.Clear();
        if (localIndex == 0)
        {
            hand.AddRange(latestHostHand);
        }
        else if (localIndex == 1)
        {
            hand.AddRange(latestGuestHand);
        }

        RenderMultiplayerHands();
    }

    private void ResolveSlotsForLocalPlayerSide()
    {
        if (resolvedLocalSideSlots) return;
        if (!MultiplayerRuntime.IsMultiplayerSession) return;

        CardSlotUI[] all = FindObjectsByType<CardSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return;

        List<(CardSlotUI slot, float x)> left = new List<(CardSlotUI slot, float x)>();
        List<(CardSlotUI slot, float x)> right = new List<(CardSlotUI slot, float x)>();

        foreach (CardSlotUI slot in all)
        {
            if (slot == null) continue;

            RectTransform rt = slot.GetComponent<RectTransform>();
            if (rt == null) continue;

            float x = rt.anchoredPosition.x;
            if (Mathf.Abs(x) < 0.001f)
            {
                x = rt.position.x;
            }

            if (x < 0f)
            {
                left.Add((slot, x));
            }
            else
            {
                right.Add((slot, x));
            }
        }

        if (left.Count == 0 || right.Count == 0) return;

        left.Sort((a, b) => a.x.CompareTo(b.x));
        right.Sort((a, b) => a.x.CompareTo(b.x));

        leftSideSlots.Clear();
        rightSideSlots.Clear();

        foreach (var p in left)
        {
            leftSideSlots.Add(p.slot);
        }

        foreach (var p in right)
        {
            rightSideSlots.Add(p.slot);
        }

        // Host(Player0)=RIGHT, Guest(Player1)=LEFT
        int localIndex = GetEffectiveLocalPlayerIndex();
        cardSlots = localIndex == 0 ? rightSideSlots : leftSideSlots;

        resolvedLocalSideSlots = true;
    }

    private void RenderMultiplayerHands()
    {
        if (!MultiplayerRuntime.IsMultiplayerSession || !resolvedLocalSideSlots)
        {
            return;
        }

        int localIndex = GetEffectiveLocalPlayerIndex();

        ApplyHandToSlots(rightSideSlots, latestHostHand, localIndex == 0);
        ApplyHandToSlots(leftSideSlots, latestGuestHand, localIndex == 1);
    }

    private static int GetEffectiveLocalPlayerIndex()
    {
        if (MultiplayerRuntime.LocalPlayerIndex == 0 || MultiplayerRuntime.LocalPlayerIndex == 1)
        {
            return MultiplayerRuntime.LocalPlayerIndex;
        }

        // Fallback when SyncVar index hasn't arrived yet.
        if (NetworkServer.active && NetworkClient.active)
        {
            return 0; // host
        }

        if (NetworkClient.active)
        {
            return 1; // guest
        }

        return -1;
    }

    private static void ApplyHandToSlots(List<CardSlotUI> slots, List<CardData> cards, bool interactable)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            CardSlotUI slot = slots[i];
            if (slot == null) continue;

            bool hasCard = cards != null && i < cards.Count && cards[i] != null;
            slot.gameObject.SetActive(hasCard);

            if (hasCard)
            {
                slot.SetCard(cards[i]);
            }

            Button button = slot.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = interactable && hasCard;
            }
        }
    }

    public int FindHandIndex(CardData card)
    {
        if (card == null) return -1;
        return hand.FindIndex(c => c == card);
    }

    public int FindSlotIndex(CardSlotUI slot)
    {
        if (slot == null) return -1;
        return cardSlots.IndexOf(slot);
    }
}
