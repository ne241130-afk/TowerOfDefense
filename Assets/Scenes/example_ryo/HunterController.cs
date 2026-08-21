using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ハンター1体を制御するコンポーネント。
/// TurnManagerに登録され、毎ターン以下の順で行動する:
/// 1. 時給制の場合は一定ターンごとに費用を消費。
/// 2. actionRadiusが設定されている場合はそのエリア内の動物のみをターゲットにする。
///    0以下ならエリア制限なしでdetectionRangeで検知する。
/// 3. ターゲットに隣接していれば捕獲し、そうでなければエリア内でA*追跡移動する。
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

    /// <summary>現在追跡中のターゲット動物。nullなら未設定。</summary>
    private AnimalController currentTarget;

    /// <summary>時給制の支払いカウンタ。</summary>
    private int paymentTurnCounter = 0;

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
    /// 支払い処理 → ターゲット選定 → 捕獲または追跡移動の順に実行する。
    /// </summary>
    public void OnTurnTick()
    {
        HandlePayment();

        // ターゲットが破棄されたまたはエリア外に移動したらリセット
        if (currentTarget == null || !IsInActionArea(currentTarget.CurrentCell))
        {
            currentTarget = null;
            currentTarget = FindNearestAnimalInRange();
        }

        if (currentTarget == null) return; // ターゲット不在 → 待機

        if (ChebyshevDistance(CurrentCell, currentTarget.CurrentCell) <= 1)
        {
            Capture(currentTarget);
        }
        else
        {
            ChaseTarget();
        }
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
    /// actionRadiusが設定されている場合はhomeCellからのエリア内の動物のみを対象とする。
    /// 0以下ならエリア制限なしでdetectionRange内の最近働を返す。
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
    /// ターゲットをCaptureCounterに加算し、GameObjectを破棄してターゲットをリセットする。
    /// </summary>
    private void Capture(AnimalController target)
    {
        Debug.Log($"{data.hunterName} が {target.Stats.animalName} を捕獲した!");
        CaptureCounter.Instance?.AddCapture();

        // WaveManagerにも捕獲を通知
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.AddCapture();
        }

        Destroy(target.gameObject);
        currentTarget = null;
    }

    /// <summary>
    /// A*でターゲットへの経路を求め、squaresPerTurn分だけ1マスずつ移動する。
    /// エリア外には踏み出さないようisWalkableで制約する。
    /// </summary>
    private void ChaseTarget()
    {
        var goals = new List<Vector3Int> { currentTarget.CurrentCell };

        List<Vector3Int> path = AStarPathfinder.FindPath(
            CurrentCell,
            goals,
            cell => FieldGridConfig.Instance.IsWalkable(cell) && IsInActionArea(cell),
            _ => false,
            _ => 1f
        );

        if (path == null || path.Count < 2) return; // 経路なし → 待機

        int steps = Mathf.Min(data.squaresPerTurn, path.Count - 1);
        for (int i = 0; i < steps; i++)
        {
            Vector3Int nextCell = path[i + 1];
            CurrentCell = nextCell;
            transform.position = FieldGridConfig.Instance.grid.GetCellCenterWorld(nextCell);
        }
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
