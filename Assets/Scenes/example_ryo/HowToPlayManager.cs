using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HowToPlayManager : MonoBehaviour
{
    [Header("Slides")]
    [SerializeField] private GameObject[] slides;

    [Header("Buttons")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backButton;

    [Header("Scene")]
    [SerializeField] private string titleSceneName = "Title_ryo";

    [Header("Optional")]
    [SerializeField] private TMP_Text pageIndicator;

    // 現在表示しているスライド番号を0始まりで管理する
    private int currentSlideIndex;

    private void Awake()
    {
        // Inspector で割り当てたボタンに処理を登録する
        RegisterButton(previousButton, ShowPreviousSlide);
        RegisterButton(nextButton, ShowNextSlide);
        RegisterButton(backButton, ReturnToTitle);
    }

    private void Start()
    {
        // 起動時は必ず1枚目だけを表示する
        ShowSlide(0);
    }

    private void OnDestroy()
    {
        // 重複登録を避けるため、破棄時に登録を解除する
        UnregisterButton(previousButton, ShowPreviousSlide);
        UnregisterButton(nextButton, ShowNextSlide);
        UnregisterButton(backButton, ReturnToTitle);
    }

    // 次のスライドへ進む
    public void ShowNextSlide()
    {
        ShowSlide(currentSlideIndex + 1);
    }

    // 前のスライドへ戻る
    public void ShowPreviousSlide()
    {
        ShowSlide(currentSlideIndex - 1);
    }

    // タイトル画面へ戻る
    public void ReturnToTitle()
    {
        if (string.IsNullOrWhiteSpace(titleSceneName))
        {
            Debug.LogWarning($"{nameof(HowToPlayManager)}: titleSceneName is empty.", this);
            return;
        }

        SceneFader_ryo.FadeToScene(titleSceneName);
    }

    // スライド切り替え処理を1つにまとめる
    private void ShowSlide(int targetIndex)
    {
        if (slides == null || slides.Length == 0)
        {
            Debug.LogWarning($"{nameof(HowToPlayManager)}: slides are not assigned.", this);
            UpdatePageIndicator(0);
            UpdateButtonState(0);
            return;
        }

        currentSlideIndex = Mathf.Clamp(targetIndex, 0, slides.Length - 1);

        for (int i = 0; i < slides.Length; i++)
        {
            if (slides[i] != null)
            {
                // 現在のスライドだけを表示し、それ以外は非表示にする
                slides[i].SetActive(i == currentSlideIndex);
            }
        }

        UpdatePageIndicator(slides.Length);
        UpdateButtonState(slides.Length);
    }

    // ページ番号表示を更新する
    private void UpdatePageIndicator(int totalSlides)
    {
        if (pageIndicator == null)
        {
            return;
        }

        if (totalSlides <= 0)
        {
            pageIndicator.text = "0 / 0";
            return;
        }

        pageIndicator.text = $"{currentSlideIndex + 1} / {totalSlides}";
    }

    // 端のページでは進みすぎ・戻りすぎを防ぐ
    private void UpdateButtonState(int totalSlides)
    {
        if (previousButton != null)
        {
            previousButton.interactable = totalSlides > 0 && currentSlideIndex > 0;
        }

        if (nextButton != null)
        {
            nextButton.interactable = totalSlides > 0 && currentSlideIndex < totalSlides - 1;
        }
    }

    // ボタンが設定されている場合だけ安全に登録する
    private void RegisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // ボタンが設定されている場合だけ安全に解除する
    private void UnregisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }
}