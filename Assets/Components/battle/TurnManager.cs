using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ターンごとに行動する対象が実装するインターフェース。
/// 動物だけでなく、将来的にハンターの自動行動なども乗せられる。
/// </summary>
public interface ITurnActor
{
    void OnTurnTick();
}

/// <summary>
/// 「1ターン進める」タイミングを一元管理するシングルトン。
/// 実際にどのタイミングで1ターン経過とみなすか(一定秒数ごと/Wave進行に連動など)は
/// 呼び出し側(GameManagerやWaveManager)がAdvanceTurn()を呼ぶことで決める。
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Tooltip("AdvanceTurnを自動で呼ぶ場合の間隔(秒)。0以下なら自動実行しない。")]
    public float autoTurnInterval = 0f;

    [SerializeField, Min(0.1f)] private float timeTurnInterval = 1f;

    public bool IsTimeTurnEnabled { get; private set; }
    public float TimeTurnInterval => Mathf.Max(0.1f, timeTurnInterval);

    private readonly List<ITurnActor> actors = new List<ITurnActor>();
    private float autoTimer = 0f;

    public int CurrentTurn { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!IsTimeTurnEnabled && autoTurnInterval <= 0f) return;

        autoTimer += Time.deltaTime;
        float interval = IsTimeTurnEnabled ? Mathf.Max(0.1f, timeTurnInterval) : autoTurnInterval;
        if (autoTimer >= interval)
        {
            autoTimer = 0f;
            AdvanceTurn(IsTimeTurnEnabled);
        }
    }

    public void Register(ITurnActor actor)
    {
        if (!actors.Contains(actor)) actors.Add(actor);
    }

    public void Unregister(ITurnActor actor)
    {
        actors.Remove(actor);
    }

    /// <summary>
    /// 1ターン進める。登録されている全アクターのOnTurnTickを呼ぶ。
    /// ターン中に動物が捕獲されてUnregisterされても安全なようスナップショットを取る。
    /// </summary>
    public void SetTimeTurnEnabled(bool enabled)
    {
        IsTimeTurnEnabled = enabled;
        autoTimer = 0f;
    }

    public void AdvanceTurn()
    {
        AdvanceTurn(false);
    }

    private void AdvanceTurn(bool fromTimeTurn)
    {
        if (IsTimeTurnEnabled && !fromTimeTurn) return;
        CurrentTurn++;

        var snapshot = new List<ITurnActor>(actors);
        foreach (var actor in snapshot)
        {
            actor.OnTurnTick();
        }
    }
}
