using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

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
    [Tooltip("シングルプレイで使用する枠。マルチプレイでは下の左右リストを使います。")]
    public List<CardSlotUI> cardSlots = new List<CardSlotUI>();

    [Header("マルチプレイ手札UI")]
    [Tooltip("ホスト(Player 0)の手札を表示する右側4枠")]
    public List<CardSlotUI> hostCardSlots = new List<CardSlotUI>();
    [Tooltip("ゲスト(Player 1)の手札を表示する左側4枠")]
    public List<CardSlotUI> guestCardSlots = new List<CardSlotUI>();

    [Header("設定")]
    public int handSize = 4;

    [Header("リロード設定")]
    public int reloadCost = 50;
    
    // 最後にリロードしたターンの記憶
    private int lastReloadTurn = -1;

    // ゲーム開始時の山札を保存
    private List<CardData> initialDeck = new List<CardData>();

    private bool networkControlledMode;
    private bool hasReceivedPrivateHand;
    private int lastRenderedPlayerIndex = -99;
    private List<CardData> latestPrivateHand = new List<CardData>();
    private List<CardData> latestHostHand = new List<CardData>();
    private List<CardData> latestGuestHand = new List<CardData>();

    private void Start()
    {
        // A networked scene has two server-owned hands. Do not draw a third,
        // local-only hand into the serialized cardSlots before sync arrives.
        if (Mirror.NetworkClient.active || Mirror.NetworkServer.active)
        {
            networkControlledMode = true;
        }

        if (networkControlledMode)
        {
            ApplyHandToSlots(hostCardSlots, null, false);
            ApplyHandToSlots(guestCardSlots, null, false);
            RenderMultiplayerHands();
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

        int currentPlayerIndex = GetEffectiveLocalPlayerIndex();
        if (currentPlayerIndex != lastRenderedPlayerIndex)
        {
            lastRenderedPlayerIndex = currentPlayerIndex;
            ApplyPrivateHandToResolvedPlayer(currentPlayerIndex);
            RenderMultiplayerHands();
            return;
        }

        RenderMultiplayerHands();
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
    /// シングルプレイでは1ターンに1回無料。マルチプレイではサーバーへ依頼し、設定コストを支払う。
    /// </summary>
    public void ReloadHand()
    {
        if (networkControlledMode)
        {
            if (!MultiplayerRuntime.IsMultiplayerSession)
            {
                Debug.LogWarning("[TRACE][Reload] Multiplayer hand is server-controlled, but no multiplayer session is active.");
                return;
            }

            if (!MultiplayerActionClient.TrySendReloadHand(out string reason))
            {
                MultiplayerRuntime.SetStatus("Reload failed: " + reason);
                Debug.LogWarning("[TRACE][Reload] request send failed: " + reason);
                return;
            }

            MultiplayerRuntime.SetStatus("Reload requested");
            return;
        }
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

        int localIndex = GetEffectiveLocalPlayerIndex();
        List<CardData> source = newHand ?? new List<CardData>();
        string handIds = string.Join(",", source.Select(c => c != null ? c.cardName : "<null>"));
        Debug.Log($"[TRACE][HandSync] SetHandFromNetwork localIndex={localIndex} incomingCount={source.Count} handIds={handIds}");

        // PrivateState is authoritative for this local player's own hand. Update
        // only that player's display side; HandsSync supplies the opponent side.
        hasReceivedPrivateHand = true;
        latestPrivateHand = new List<CardData>(source);
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

        latestHostHand = hostHand != null ? new List<CardData>(hostHand) : new List<CardData>();
        latestGuestHand = guestHand != null ? new List<CardData>(guestHand) : new List<CardData>();
        Debug.Log($"[TRACE][HandsDisplay] received host={latestHostHand.Count} guest={latestGuestHand.Count}");

        int localIndex = GetEffectiveLocalPlayerIndex();
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

    private void RenderMultiplayerHands()
    {
        if (!networkControlledMode) return;

        int localIndex = GetEffectiveLocalPlayerIndex();
        // Visibility comes from the two synchronized hand lists. The local
        // player index controls input only; it must not hide either player's cards
        // while that identity's SyncVar is still arriving.
        ApplyHandToSlots(hostCardSlots, latestHostHand, localIndex == 0);
        ApplyHandToSlots(guestCardSlots, latestGuestHand, localIndex == 1);
    }

    private void ApplyPrivateHandToResolvedPlayer(int localIndex)
    {
        // A private hand can arrive before the local PlayerIndex SyncVar. Reapply
        // it to the correct side once that authoritative index becomes available.
        if (!hasReceivedPrivateHand) return;

        if (localIndex == 0)
        {
            latestHostHand = new List<CardData>(latestPrivateHand);
        }
        else if (localIndex == 1)
        {
            latestGuestHand = new List<CardData>(latestPrivateHand);
        }
    }

    private static int GetEffectiveLocalPlayerIndex()
    {
        return MultiplayerRuntime.GetEffectiveLocalPlayerIndex();
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
        if (MultiplayerRuntime.IsMultiplayerSession)
        {
            int localIndex = GetEffectiveLocalPlayerIndex();
            List<CardSlotUI> localSlots = localIndex == 0 ? hostCardSlots : localIndex == 1 ? guestCardSlots : null;
            return localSlots != null ? localSlots.IndexOf(slot) : -1;
        }

        return cardSlots.IndexOf(slot);
    }

    public CardData GetHandCardAt(int index)
    {
        if (index < 0 || index >= hand.Count) return null;
        return hand[index];
    }
}
