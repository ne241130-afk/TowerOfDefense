using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ハンター1体を制御するコンポーネント。
/// TurnManagerに登録され、毎ターン以下の順で行動する:
/// 1. 時給制の場合は一定ターンごとに費用を消費。
/// 2. 攻撃間隔を満たしたターンのみ、攻撃範囲内の最近接1体を攻撃する。
/// 3. 動物の捕獲HPが0以下(捕獲条件到達)になったら捕獲処理を実行する。
/// </summary>
public class HunterController : MonoBehaviour, ITurnActor
{
    [Header("ステータス")]
    /// <summary>ハンターの各種データ。</summary>
    public HunterData data = new HunterData();

    /// <summary>現在ハンターがいるセル座標。</summary>
    public Vector3Int CurrentCell { get; private set; }

    /// <summary>ハンターが配置されたセル座標。actionRadiusの中心点。</summary>
    public Vector3Int HomeCell { get; private set; }

    /// <summary>時給制の支払いカウンタ。</summary>
    private int paymentTurnCounter = 0;

    /// <summary>攻撃間隔用のターンカウンタ。</summary>
    private int attackTurnCounter = 0;

    /// <summary>時限ハンター用の寿命ターン。0以下なら無制限。</summary>
    private int remainingLifetimeTurns = 0;

    private void Start()
    {
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            Debug.LogError($"{name}: FieldGridConfigが見つかりません。シーンに配置してください。", this);
            return;
        }

        CurrentCell = FieldGridConfig.Instance.grid.WorldToCell(transform.position);
        HomeCell    = CurrentCell; // 配置地点をホームとして記憶
        TurnManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
    }

    /// <summary>
    /// TurnManagerから毎ターン呼ばれるメイン処理。
    /// 支払い処理 → 攻撃間隔判定 → ターゲット選定 → 攻撃の順で実行する。
    /// </summary>
    public void OnTurnTick()
    {
        if (remainingLifetimeTurns > 0)
        {
            remainingLifetimeTurns--;
            if (remainingLifetimeTurns <= 0)
            {
                Destroy(gameObject);
                return;
            }
        }

        HandlePayment();
        if (!CanAttackThisTurn()) return;

        var target = FindNearestAnimalInRange();
        if (target == null) return; // 範囲内に動物なし → 待機

        Attack(target);
    }

    /// <summary>
    /// このハンターを時限モードにし、指定ターン経過後に消滅させる。
    /// </summary>
    public void ConfigureLifetime(int lifetimeTurns)
    {
        remainingLifetimeTurns = Mathf.Max(0, lifetimeTurns);
    }

    private bool CanAttackThisTurn()
    {
        int interval = Mathf.Max(1, data.attackIntervalTurns);
        attackTurnCounter++;
        if (attackTurnCounter < interval) return false;

        attackTurnCounter = 0;
        return true;
    }

    /// <summary>
    /// 時給制の場合、paymentIntervalTurnsターンごとにログを出力し、EconomyManagerでコストを消費する。
    /// </summary>
    private void HandlePayment()
    {
        if (data.hireType != HunterHireType.HourlyWage) return;

        paymentTurnCounter++;
        if (paymentTurnCounter < data.paymentIntervalTurns) return;
        paymentTurnCounter = 0;

        Debug.Log($"{data.hunterName}: 時給({data.cost})を消費");
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.TrySpend(data.cost);
        }
    }

    /// <summary>
    /// ターゲット候補を探す。
    /// actionRadiusが設定されている場合はhomeCellからのエリア内のみを対象とする。
    /// 0以下ならdetectionRange内を対象とする。
    /// </summary>
    private AnimalController FindNearestAnimalInRange()
    {
        if (AnimalOccupancyMap.Instance == null) return null;

        AnimalController nearest = null;
        int nearestDist = int.MaxValue;
        bool hasActionArea  = data.actionRadius > 0;
        bool unlimitedDetect = data.detectionRange <= 0;

        foreach (AnimalController animal in AnimalOccupancyMap.Instance.All)
        {
            if (animal == null) continue;

            // actionRadiusが設定されている場合はエリア内の動物のみを対象にする
            if (hasActionArea && !IsInActionArea(animal.CurrentCell)) continue;

            // actionRadiusが無制限ならdetectionRangeでフィルタリング
            if (!hasActionArea && !unlimitedDetect)
            {
                int detectDist = ChebyshevDistance(CurrentCell, animal.CurrentCell);
                if (detectDist > data.detectionRange) continue;
            }

            int dist = ChebyshevDistance(CurrentCell, animal.CurrentCell);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = animal;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 1体だけ攻撃し、捕獲条件到達時のみ捕獲処理を実行する。
    /// </summary>
    private void Attack(AnimalController target)
    {
        int damage = Mathf.Max(1, data.attackPower);
        bool captured = target.ApplyCaptureDamage(damage);
        if (!captured) return;

        Debug.Log($"{data.hunterName} が {target.Stats.animalName} を捕獲した!");
        CaptureCounter.Instance?.AddCapture();

        // WaveManagerにも捕獲を通知
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.AddCapture();
        }

        Destroy(target.gameObject);
    }

    /// <summary>
    /// 指定セルがハンターの行動エリア内かどうかを返す。
    /// actionRadiusが0以下なら常にtrue(無制限)。
    /// </summary>
    private bool IsInActionArea(Vector3Int cell)
        => data.actionRadius <= 0 || ChebyshevDistance(HomeCell, cell) <= data.actionRadius;

    /// <summary>
    /// 2セル間のChebyshev距離を返す。
    /// </summary>
    private static int ChebyshevDistance(Vector3Int a, Vector3Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    /// <summary>
    /// Scene View上にエリア・検知範囲をギズモ表示する。
    /// アクションエリア: 青(homeCell中心) / 検知範囲: オレンジ(現在地中心)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null) return;

        var grid = FieldGridConfig.Instance.grid;

        // アクションエリア（青）― homeCell中心
        if (data.actionRadius > 0)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f);
            Vector3Int home = Application.isPlaying
                ? HomeCell
                : grid.WorldToCell(transform.position);

            for (int dx = -data.actionRadius; dx <= data.actionRadius; dx++)
            for (int dy = -data.actionRadius; dy <= data.actionRadius; dy++)
            {
                Vector3 worldPos = grid.GetCellCenterWorld(home + new Vector3Int(dx, dy, 0));
                Gizmos.DrawCube(worldPos, grid.cellSize * 0.9f);
            }
        }

        // 検知範囲（オレンジ）― 現在地中心（actionRadiusが0以下の時のみ表示）
        if (data.actionRadius <= 0 && data.detectionRange > 0)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Vector3Int center = grid.WorldToCell(transform.position);

            for (int dx = -data.detectionRange; dx <= data.detectionRange; dx++)
            for (int dy = -data.detectionRange; dy <= data.detectionRange; dy++)
            {
                Vector3 worldPos = grid.GetCellCenterWorld(center + new Vector3Int(dx, dy, 0));
                Gizmos.DrawCube(worldPos, grid.cellSize * 0.9f);
            }
        }
    }
}
