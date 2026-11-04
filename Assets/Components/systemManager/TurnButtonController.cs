using UnityEngine;

public class TurnButtonController : MonoBehaviour
{
    public void OnTurnButton()
    {
        Debug.Log("ターンボタンが押されました");

        if (MultiplayerRuntime.IsMultiplayerSession)
        {
            if (!MultiplayerRuntime.IsGameplayReady)
            {
                MultiplayerRuntime.SetStatus("接続完了まで待機してください");
                return;
            }

            MultiplayerPlayerIdentity identity = MultiplayerRuntime.LocalPlayerIdentity;
            if (identity != null)
            {
                identity.CmdRequestSkipTurn();
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