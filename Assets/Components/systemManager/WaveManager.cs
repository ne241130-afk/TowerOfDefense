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
    public int CaptureCount => captureCount;

    [Header("イベント（Inspector で UI 等を登録）")]
    public UnityEvent<int> OnWaveStarted;
    public UnityEvent<int> OnWaveCompleted;
    public UnityEvent<int> OnTurnChanged;

    private bool isClearingWave = false;

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
        if (MultiplayerRuntime.IsMultiplayerSession && !Mirror.NetworkServer.active) return;
        captureCount++;

        CheckWaveClear();
    }

    public void AddEscape()
    {
        if (MultiplayerRuntime.IsMultiplayerSession && !Mirror.NetworkServer.active) return;
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
        if (MultiplayerRuntime.IsMultiplayerSession && !Mirror.NetworkServer.active) return;
        turnInWave++;
        Debug.Log("WaveManager: ターン " + turnInWave + " / " + turnsPerWave);
        OnTurnChanged?.Invoke(turnInWave);
        CheckWaveClear();
    }

    public void ApplyAuthoritativeNetworkState(int wave, int turn, int captures, int escapes)
    {
        int authoritativeWave = Mathf.Max(1, wave);
        bool waveChanged = currentWave != authoritativeWave;
        if (waveChanged && authoritativeWave % 5 == 0)
            CardPlacementController.Instance?.ClearPlacedBoardItems();

        currentWave = authoritativeWave;
        turnInWave = Mathf.Max(0, turn);
        captureCount = Mathf.Max(0, captures);
        escapeCount = Mathf.Max(0, escapes);
        if (waveChanged) OnWaveStarted?.Invoke(currentWave);
        OnTurnChanged?.Invoke(turnInWave);
    }

    public void ResetState(int startWave = 1)
    {
        currentWave = Mathf.Max(1, startWave);
        turnInWave = 0;
    }

    private void CheckWaveClear()
    {
        // すでにクリア処理中なら何もしない
        if (isClearingWave) return;

        if (turnInWave < turnsPerWave &&
            captureCount < capturesPerWave)
        {
            return;
        }

        isClearingWave = true;

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

        if (currentWave % 5 == 0)
        {
            CardPlacementController.Instance?.ClearPlacedBoardItems();
        }

        OnWaveStarted?.Invoke(currentWave);

        isClearingWave = false;
    }
}
