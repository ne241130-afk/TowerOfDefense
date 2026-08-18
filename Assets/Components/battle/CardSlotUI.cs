using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手札の1枚のカードを表すUIコンポーネント。
/// Button + Image 構成のGameObjectにアタッチするだけで動作する。
///
/// セットアップ:
///   1. cardData にカードデータを設定する(CardDeckManager使用時は不要)
///   2. iconImage は未設定でも可。未設定の場合、同じGameObjectのImageを自動使用する
///   3. nameText にカード名表示用のTextを割り当てる(任意)
///   4. selectedFrame に選択中を示すGameObjectを割り当てる(任意)
///   5. ButtonのOnClickに OnClickCard() を登録する
/// </summary>
public class CardSlotUI : MonoBehaviour
{
    [Header("カードデータ")]
    public CardData cardData;

    [Header("見た目の参照")]
    [Tooltip("未設定の場合、このGameObject上のImageコンポーネントを自動で使用する")]
    public Image iconImage;
    public Text nameText;
    [Tooltip("選択中に表示する枠など(任意)")]
    public GameObject selectedFrame;

    /// <summary>アイコン表示に使うImageの解決済み参照。</summary>
    private Image resolvedIconImage;

    private void Awake()
    {
        // iconImage が Inspector で未設定なら自身の Image コンポーネントを使う
        resolvedIconImage = iconImage != null ? iconImage : GetComponent<Image>();
    }

    private void Start()
    {
        ApplyCardVisual();
        SetSelectedVisual(false);
    }

    /// <summary>
    /// CardDeckManagerからカードを設定する。
    /// </summary>
    public void SetCard(CardData data)
    {
        cardData = data;
        ApplyCardVisual();
        SetSelectedVisual(false);
    }

    /// <summary>
    /// cardData の内容をボタンの見た目に反映する。
    /// </summary>
    private void ApplyCardVisual()
    {
        if (cardData == null) return;

        if (resolvedIconImage != null)
        {
            resolvedIconImage.sprite = cardData.icon;
            // sprite が null のときは透明にして背景色だけ表示
            resolvedIconImage.color = cardData.icon != null ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        }

        if (nameText != null)
        {
            nameText.text = cardData.cardName;
        }
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
