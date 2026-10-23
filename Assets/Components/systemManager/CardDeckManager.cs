using System.Collections.Generic;
using UnityEngine;

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

    private void Start()
    {
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

    /// <summary>
    /// Wave終了時に呼ばれる。手札に招き猫が残っていれば所持金を1.5倍にする。
    /// 複数枚ある場合は1枚ごとに1.5倍を適用する。
    /// </summary>
    private void OnWaveCompleted(int completedWave)
    {
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
}
