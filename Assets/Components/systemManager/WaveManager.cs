using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ターン進行に追従して Wave/Turn を管理するシンプルなマネージャ。
/// </summary>
public class WaveManager : MonoBehaviour, ITurnActor
{
    public static WaveManager Instance { get; private set; }

    [Header("状態（読み取り専用）")]
    public int currentWave = 1;
    public int turnInWave = 0;

    [Header("Waveクリア条件")]
    public int turnsPerWave = 20; /// 何ターン経過か
    public int capturesPerWave = 5; /// 捕獲数

    private int captureCount = 0; /// 
    private int escapeCount = 0; /// 脱走数
    public int EscapeCount => escapeCount;

    [Header("イベント（Inspector で UI 等を登録）")]
    public UnityEvent<int> OnWaveStarted;
    public UnityEvent<int> OnWaveCompleted;
    public UnityEvent<int> OnTurnChanged;

    private void Awake()
    {
        Instance = this;
    }

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

    public void AddEscape()
    {
        escapeCount++;

        Debug.Log($"脱走数: {escapeCount}");

        if (escapeCount >= 5)
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

    public void OnTurnTick()
    {
        turnInWave++;
        Debug.Log("WaveManager: ターン " + turnInWave + " / " + turnsPerWave);
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

            // Wave 20をクリアしたらゲームクリア
            if (currentWave >= 20)
            {
                if (SimpleGameManager.Instance != null)
                {
                    SimpleGameManager.Instance.GameClear();
                }
                else
                {
                    Debug.LogError("SimpleGameManager が見つかりません。");
                }

                return;
            }

            currentWave++;
            turnInWave = 0;
            captureCount = 0;

            OnWaveStarted?.Invoke(currentWave);
        }
    }
}