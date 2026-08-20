using UnityEngine;

public class TurnButtonController : MonoBehaviour
{
    public void OnTurnButton()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.AdvanceTurn();
        }
        else
        {
            Debug.LogError("TurnManager が見つかりません。");
        }
    }
}