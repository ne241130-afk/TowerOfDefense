
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HowToPlayPanelController : MonoBehaviour
{
    [Header("遊び方パネル")]
    [SerializeField] private GameObject howToPlayPanel;

    [Header("ページ表示")]
    [SerializeField] private Image pageImage;
    [SerializeField] private TMP_Text pageNumberText;

    [Header("ページ画像")]
    [SerializeField] private Sprite[] pages;

    [Header("ボタン")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    private int currentPage = 0;

    // 遊び方パネルを開く
    public void OpenHowToPlay()
    {
        howToPlayPanel.SetActive(true);

        currentPage = 0;
        UpdatePage();
    }

    // パネルを閉じる
    public void CloseHowToPlay()
    {
        howToPlayPanel.SetActive(false);
    }

    // 次のページ
    public void NextPage()
    {
        if (currentPage < pages.Length - 1)
        {
            currentPage++;
            UpdatePage();
        }
    }

    // 前のページ
    public void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdatePage();
        }
    }

    // ページ表示を更新
    private void UpdatePage()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning("遊び方の画像が登録されていません。");
            return;
        }

        pageImage.sprite = pages[currentPage];

        pageNumberText.text =
            $"{currentPage + 1} / {pages.Length}";

        previousButton.interactable = currentPage > 0;
        nextButton.interactable =
            currentPage < pages.Length - 1;
    }
}
