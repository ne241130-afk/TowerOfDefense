
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HowToPlayController : MonoBehaviour
{
    [Header("遊び方画面")]
    public GameObject howToPlayPanel;

    [Header("ページ表示")]
    public Image pageImage;
    public TMP_Text pageNumberText;

    [Header("ページ画像")]
    public Sprite[] pages;

    [Header("ページ切り替えボタン")]
    public Button previousButton;
    public Button nextButton;

    private int currentPage = 0;

    // 非表示にするマルチプレイボタン
    private GameObject multiButton;

    private void Start()
    {
        // Canvas直下にあるMultiButtonを探す
        Transform multiButtonTransform = transform.Find("MultiButton");

        if (multiButtonTransform != null)
        {
            multiButton = multiButtonTransform.gameObject;
        }
    }

    // 遊び方ボタンを押したとき
    public void OnHowToPlayButton()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogError("遊び方の画像が登録されていません。");
            return;
        }

        // マルチプレイボタンを非表示
        if (multiButton != null)
        {
            multiButton.SetActive(false);
        }

        howToPlayPanel.SetActive(true);

        currentPage = 0;
        UpdatePage();
    }

    // 次へボタンを押したとき
    public void OnNextButton()
    {
        if (currentPage < pages.Length - 1)
        {
            currentPage++;
            UpdatePage();
        }
    }

    // 前へボタンを押したとき
    public void OnPreviousButton()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdatePage();
        }
    }

    // 戻るボタンを押したとき
    public void OnBackButton()
    {
        howToPlayPanel.SetActive(false);

        // マルチプレイボタンを再表示
        if (multiButton != null)
        {
            multiButton.SetActive(true);
        }
    }

    // ページ表示を更新
    private void UpdatePage()
    {
        pageImage.sprite = pages[currentPage];

        pageNumberText.text =
            $"{currentPage + 1} / {pages.Length}";

        previousButton.interactable = currentPage > 0;
        nextButton.interactable = currentPage < pages.Length - 1;
    }
}
