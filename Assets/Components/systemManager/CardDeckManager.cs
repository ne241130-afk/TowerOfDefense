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
