using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ターン進行に追従して Wave/Turn を管理するシンプルなマネージャ。
/// </summary>
public class WaveManager : MonoBehaviour, ITurnActor
{

    [Header("状態（読み取り専用）")]
    public int currentWave = 1;
    public int turnInWave = 0;

    [Header("Waveクリア条件")]
    public int turnsPerWave = 5; /// 何ターン経過か
    public int capturesPerWave = 3; /// 捕獲数

    private int captureCount = 0; /// 

    [Header("イベント（Inspector で UI 等を登録）")]
    public UnityEvent<int> OnWaveStarted;
    public UnityEvent<int> OnWaveCompleted;
    public UnityEvent<int> OnTurnChanged;

    private void Start()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
        }
        else
        {
            Debug.LogWarning("WaveManager: TurnManager が見つかりません");
        }
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
    }

    public void AddCapture()
    {
        captureCount++;

        CheckWaveClear();
    }

    public void OnTurnTick()
    {
        turnInWave++;
        OnTurnChanged?.Invoke(turnInWave);
        CheckWaveClear();
    }

    public void ResetState(int startWave = 1)
    {
        currentWave = Mathf.Max(1, startWave);
        turnInWave = 0;
    }

    private void CheckWaveClear()
    {
        if (turnInWave >= turnsPerWave ||
            captureCount >= capturesPerWave)
        {
            OnWaveCompleted?.Invoke(currentWave);

            currentWave++;
            turnInWave = 0;
            captureCount = 0;

            OnWaveStarted?.Invoke(currentWave);
        }
    }
}