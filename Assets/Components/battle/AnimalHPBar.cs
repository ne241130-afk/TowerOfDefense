
using UnityEngine;
using UnityEngine.UI;

public class AnimalHPBar : MonoBehaviour
{
    [Header("HPバーの赤いゲージ")]
    [SerializeField] private Image fillImage;

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

        requiredHits = Mathf.Max(1, requiredHits);

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
}

