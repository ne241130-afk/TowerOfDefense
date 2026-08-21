using UnityEngine;
using UnityEngine.UI;

public class CardDescriptionUI : MonoBehaviour
{
    public static CardDescriptionUI Instance { get; private set; }

    [Header("説明欄")]
    public Text descriptionText;

    private void Awake()
    {
        Instance = this;
    }

    public void ShowCard(CardData card)
    {
        if (card == null) return;

        if (descriptionText != null)
        {
            descriptionText.text =
                card.cardName + "\n\n" +
                card.description + "\n\n" +
                "コスト：" + card.cost;
        }
    }
}