using UnityEngine;
using UnityEngine.SceneManagement;

public class GameClearController : MonoBehaviour
{
    public void OnTitleButton()
    {
        Debug.Log("タイトルに戻るボタンが押されました");
        SceneManager.LoadScene("StartScene");
    }
}