using UnityEngine;

public class TurnButtonController : MonoBehaviour
{
    public void OnTurnButton()
    {
        Debug.Log("ターンボタンが押されました");

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.AdvanceTurn();
            Debug.Log("ターンを進めました。現在のターン: " + TurnManager.Instance.CurrentTurn);
        }
        else
        {
            Debug.LogError("TurnManager が見つかりません。");
        }
    }
}