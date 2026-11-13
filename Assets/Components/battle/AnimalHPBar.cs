
using UnityEngine;
using UnityEngine.UI;

public class AnimalHPBar : MonoBehaviour
{
    [Header("HPバーの赤いゲージ")]
    [SerializeField] private Image fillImage;

    //HPの目盛り
    [Header("区切り線")]
    [SerializeField] private RectTransform dividerContainer;
    [SerializeField] private float dividerWidth = 2f;
    [SerializeField] private Color dividerColor = Color.white;

    //現在の区切り線の本数
    private int generatedDividerCount = -1;

    //<summary>
    //捕獲判定の回数に応じてHPバーを更新する。
    //</summary>
    public void UpdateHP(int captureHits, int requiredHits)
    {
        if (fillImage == null)
        {
            Debug.LogWarning("HPバーのFillが設定されていません。");
            return;
        }

        //最低捕獲ターンを1に
        requiredHits = Mathf.Max(1, requiredHits);

        //区切り線を生成
        CreateDividers(requiredHits);

        float remainingRatio =
             Mathf.Clamp01((float)(requiredHits - captureHits) / requiredHits);
            Debug.Log(
                $"HPバー更新後: captureHits={captureHits}, " +
                $"requiredHits={requiredHits}, " +
                $"設定値={remainingRatio}, " +
                $"実際の値={fillImage.fillAmount}"
            );

        fillImage.fillAmount = Mathf.Clamp01(remainingRatio);
    }

    /// <summary>
    /// 必要な捕獲回数に応じて区切り線を生成する。
    /// </summary>
    private void CreateDividers(int requiredHits)
    {
        if (dividerContainer == null)
        {
            Debug.LogWarning("Dividersが設定されていません。");
            return;
        }

        //すでに同じ本数の区切り線がある場合は何もしない
        int dividerCount = requiredHits - 1;

        if (generatedDividerCount == dividerCount)
        {
            return;
        }

        //古い区切り線を削除
        for (int i = dividerContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(dividerContainer.GetChild(i).gameObject);
        }

        generatedDividerCount = dividerCount;

        //区切り線を等間隔に生成
        for (int i = 1; i <= dividerCount; i++)
        {
            float position = (float)i / requiredHits;

            GameObject lineObject = new GameObject(
                "Divider_" + i,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            lineObject.transform.SetParent(dividerContainer, false);

            RectTransform rect = lineObject.GetComponent<RectTransform>();

            //横位置を必要な回数に応じて設定
            rect.anchorMin = new Vector2(position, 0f);
            rect.anchorMax = new Vector2(position, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(dividerWidth, 0f);

            Image lineImage = lineObject.GetComponent<Image>();
            
            //Fillと同じ画像を区切り線に使用
            lineImage.sprite = fillImage.sprite;


            lineImage.color = dividerColor;
            lineImage.raycastTarget = false;
        }
    }
}
