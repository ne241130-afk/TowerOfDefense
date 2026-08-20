using UnityEngine;

public class RetireController : MonoBehaviour
{
    [Header("リタイア確認画面")]
    public GameObject confirmPanel;

    // リタイアボタンを押したとき
    public void OnRetireButton()
    {
        confirmPanel.SetActive(true);
    }

    // 「いいえ」を押したとき
    public void OnCancelButton()
    {
        confirmPanel.SetActive(false);
    }

    // 「はい」を押したとき
    public void OnConfirmButton()
    {
        if (SimpleGameManager.Instance != null)
        {
            SimpleGameManager.Instance.GameOver();
        }
        else
        {
            Debug.LogError("SimpleGameManager が見つかりません。");
        }
    }
}