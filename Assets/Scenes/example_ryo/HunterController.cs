using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ハンター1体を制御するコンポーネント。
/// TurnManagerに登録され、毎ターン以下の順で行動する:
/// 1. 時給制の場合は一定ターンごとに費用を消費(ログ出力)。
/// 2. ターゲットが未設定なら、detectionRange内(0以下なら無制限)で
///    最も近い動物をChebyshev距離で探してターゲットにセットする。
/// 3. ターゲットに隣接していれば捕獲し、そうでなければA*で追跡移動する。
///
/// 動作確認用に、まずはシーンへ手動配置して使う想定
/// (雇用[所持金消費・クリックで配置]は所持金システム導入後に別途実装する)。
/// </summary>
public class HunterController : MonoBehaviour, ITurnActor
{
    [Header("ステータス")]
    /// <summary>ハンターの各種データ。</summary>
    public HunterData data = new HunterData();

    /// <summary>現在ハンターがいるセル座標。</summary>
    public Vector3Int CurrentCell { get; private set; }

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

        // ターゲットが破棄されていたらリセット
        if (currentTarget == null)
        {
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
    /// 時給制の場合、paymentIntervalTurnsターンごとにログを出力する。
    /// </summary>
    private void HandlePayment()
    {
        if (data.hireType != HunterHireType.HourlyWage) return;

        paymentTurnCounter++;
        if (paymentTurnCounter < data.paymentIntervalTurns) return;
        paymentTurnCounter = 0;

        // TODO: 所持金システム導入後、ここで data.cost を消費する処理を呼ぶ
        //       (所持金が足りなければハンターを停止させる処理なども今後追加)
        Debug.Log($"{data.hunterName}: 時給({data.cost})を消費");
    }

    /// <summary>
    /// detectionRange内(0以下なら無制限)でChebyshev距離が最小の動物を返す。
    /// 候補がいなければnull。
    /// </summary>
    private AnimalController FindNearestAnimalInRange()
    {
        if (AnimalOccupancyMap.Instance == null) return null;

        AnimalController nearest = null;
        int nearestDist = int.MaxValue;
        bool unlimited = data.detectionRange <= 0;

        foreach (AnimalController animal in AnimalOccupancyMap.Instance.All)
        {
            if (animal == null) continue;

            int dist = ChebyshevDistance(CurrentCell, animal.CurrentCell);
            if (!unlimited && dist > data.detectionRange) continue;
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
        Destroy(target.gameObject);
        currentTarget = null;
    }

    /// <summary>
    /// A*でターゲットへの経路を求め、squaresPerTurn分だけ1マスずつ移動する。
    /// 経路が見つからない場合は待機する。
    /// </summary>
    private void ChaseTarget()
    {
        var goals = new List<Vector3Int> { currentTarget.CurrentCell };

        List<Vector3Int> path = AStarPathfinder.FindPath(
            CurrentCell,
            goals,
            cell => FieldGridConfig.Instance.IsWalkable(cell),
            _ => false,   // 動物の占有マスを無視して移動可能
            _ => 1f       // 全マスのコストを均一に1とする
        );

        if (path == null || path.Count < 2) return; // 経路なし → 待機

        // squaresPerTurn分だけ経路を1マスずつ進む(path[0]が現在地)
        int steps = Mathf.Min(data.squaresPerTurn, path.Count - 1);
        for (int i = 0; i < steps; i++)
        {
            Vector3Int nextCell = path[i + 1];
            CurrentCell = nextCell;
            transform.position = FieldGridConfig.Instance.grid.GetCellCenterWorld(nextCell);
        }
    }

    /// <summary>
    /// 2セル間のChebyshev距離を返す。
    /// </summary>
    private static int ChebyshevDistance(Vector3Int a, Vector3Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    /// <summary>
    /// 検知範囲をScene View上でオレンジ半透明のギズモとして描画する(範囲無制限の場合は非表示)。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (data.detectionRange <= 0) return; // 範囲無制限時は描画しない
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        var grid = FieldGridConfig.Instance.grid;
        Vector3Int center = grid.WorldToCell(transform.position);

        for (int dx = -data.detectionRange; dx <= data.detectionRange; dx++)
        {
            for (int dy = -data.detectionRange; dy <= data.detectionRange; dy++)
            {
                Vector3 worldPos = grid.GetCellCenterWorld(center + new Vector3Int(dx, dy, 0));
                Gizmos.DrawCube(worldPos, grid.cellSize * 0.9f);
            }
        }
    }
}
