using UnityEngine;

public class TurnButtonController : MonoBehaviour
{
    public void OnTurnButton()
    {
        Debug.Log("ターンボタンが押されました");

        if (MultiplayerRuntime.IsMultiplayerSession)
        {
            if (!MultiplayerActionClient.TrySendSkipTurn(out string reason))
            {
                MultiplayerRuntime.SetStatus("TRACE: skip send failed, " + reason);
            }
            else
            {
                MultiplayerRuntime.SetStatus("TRACE: sent SkipTurnRequestMessage");
            }
            return;
        }

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