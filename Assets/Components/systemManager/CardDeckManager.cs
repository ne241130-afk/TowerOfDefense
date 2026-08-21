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

    private void Start()
    {
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
}
