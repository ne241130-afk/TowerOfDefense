using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手札の1枚のカードを表すUIコンポーネント。
/// カードプレハブ(Button + Image + Text構成)にアタッチして使う。
///
/// セットアップ:
///   1. cardData に、このカードの効果・見た目を設定する
///   2. iconImage / nameText に、カード上のアイコンと名前表示用のUI要素を割り当てる
///   3. selectedFrame に、選択中であることを示す枠などのGameObjectを割り当てる(任意)
///   4. ButtonのOnClickに、このコンポーネントのOnClickCard()を登録する
/// </summary>
public class CardSlotUI : MonoBehaviour
{
    [Header("カードデータ")]
    public CardData cardData;

    [Header("見た目の参照")]
    public Image iconImage;
    public Text nameText;
    [Tooltip("選択中に表示する枠など(任意)")]
    public GameObject selectedFrame;

    private void Start()
    {
        if (nameText != null) nameText.text = cardData.cardName;
        if (iconImage != null && cardData.icon != null) iconImage.sprite = cardData.icon;
        SetSelectedVisual(false);
    }

    /// <summary>
    /// CardDeckManagerからカードを設定する。
    /// </summary>
    public void SetCard(CardData data)
    {
        cardData = data;

        if (nameText != null)
        {
            nameText.text = cardData.cardName;
        }

        if (iconImage != null)
        {
            iconImage.sprite = cardData.icon;
        }

        SetSelectedVisual(false);
    }

    /// <summary>
    /// ButtonのOnClickイベントからこれを呼ぶ。
    /// </summary>
    public void OnClickCard()
    {
        if (CardPlacementController.Instance != null)
        {
            CardPlacementController.Instance.SelectCard(cardData, this);
        }
    }

    public void SetSelectedVisual(bool selected)
    {
        if (selectedFrame != null) selectedFrame.SetActive(selected);
    }

    /// <summary>
    /// カードを使ったときに呼ばれる。新しいカードを自動で山札から引く
    /// </summary>
    public void ConsumeCard()
    {
        if (CardDeckManager.Instance != null)
        {
            CardDeckManager.Instance.UseCard(cardData);
        }
    }
}
