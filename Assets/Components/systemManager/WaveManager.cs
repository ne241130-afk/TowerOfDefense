using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ターン進行に追従して Wave/Turn を管理するシンプルなマネージャ。
/// - TurnManager に登録しておくことで AdvanceTurn ごとに OnTurnTick が呼ばれる。
/// - Inspector で turnsPerWave を設定可能（デフォルト 5）。
/// - OnWaveStarted / OnWaveCompleted / OnTurnChanged は Inspector で UI に紐付け可能。
/// </summary>
public class WaveManager : MonoBehaviour, ITurnActor
{
    [Tooltip("1Wave に含まれるターン数")]
    public int turnsPerWave = 5;

    [Header("状態（読み取り専用）")]
    public int currentWave = 1;
    public int turnInWave = 0;

    [Header("イベント（Inspector で UI 等を登録）")]
    public UnityEvent<int> OnWaveStarted;   // 引数: waveNumber
    public UnityEvent<int> OnWaveCompleted; // 引数: waveNumber
    public UnityEvent<int> OnTurnChanged;   // 引数: turnInWave

    private void Start()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
        }
        else
        {
            Debug.LogWarning("WaveManager: TurnManager が見つかりません。Start 時に登録できませんでした。");
        }
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// TurnManager から呼ばれる（1ターン進む毎に実行される）。
    /// </summary>
    public void OnTurnTick()
    {
        turnInWave++;

        // 通常のターン更新通知
        OnTurnChanged?.Invoke(turnInWave);

        // このターンで Wave がちょうど完了する場合（例: 5ターン目）
        if (turnInWave == turnsPerWave)
        {
            OnWaveCompleted?.Invoke(currentWave);
            // Wave の切り替えは次の AdvanceTurn で行う設計（必要ならここで切り替える）
            return;
        }

        // 次の AdvanceTurn で turnInWave が turnsPerWave + 1 になる -> 波を繰り上げる処理
        if (turnInWave > turnsPerWave)
        {
            currentWave++;
            turnInWave = 1;
            OnWaveStarted?.Invoke(currentWave);
            OnTurnChanged?.Invoke(turnInWave);
        }
    }

    /// <summary>
    /// ゲーム再開やリスタート時に状態をリセットしたければ呼ぶ
    /// </summary>
    public void ResetState(int startWave = 1)
    {
        currentWave = Mathf.Max(1, startWave);
        turnInWave = 0;
    }
}