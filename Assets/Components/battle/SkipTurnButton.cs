/// <summary>
/// スキップボタン用コンポーネント。
/// Buttonに追加し、OnClickに <see cref="OnClick"/> を登録するだけで使える。
///
/// 動作: プレイヤーがカードを使わずターンを進めたいときに押す。
///       TurnManagerに登録された全アクター(動物・ハンターなど)が1ターン分行動する。
/// </summary>
public class SkipTurnButton : UnityEngine.MonoBehaviour
{
    /// <summary>
    /// Buttonのクリックイベントに登録する。
    /// </summary>
    public void OnClick()
    {
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

        TurnManager.Instance?.AdvanceTurn();
    }
}
