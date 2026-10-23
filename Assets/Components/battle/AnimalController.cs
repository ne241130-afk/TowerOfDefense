using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 動物1体を制御するコンポーネント。
/// 毎ターン、A*で現在地からゴール(FieldGridConfig.goalCells)までの経路を再計算し、1マスずつ進む。
///
/// ・他の動物が今いるマスは経路探索上ブロックされる → 動物同士は重ならない
/// ・妨害効果のあるマスはコストが上がる → 迂回できるなら迂回し、できなければ通って足止めを受ける
/// ・ゴールが複数(最上段中央2マスなど)ある場合、片方が鎖などで塞がれがちなら
///   もう片方を優先するようになる
/// </summary>
public class AnimalController : MonoBehaviour, ITurnActor
{
    [Header("ステータス")]
    public AnimalStats stats = new AnimalStats();

    public AnimalStats Stats => stats;
    public Vector3Int CurrentCell { get; private set; }

    // 足止め残りターン数(沼地・鎖などから加算される)
    private int moveDelayTurns = 0;

    // エフェクト用
    private AnimalStatusEffectUI statusEffectUI;

    //HPバー
    [Header("HPバー")]
    [SerializeField] private AnimalHPBar hpBarPrefab;
    [SerializeField] private Vector3 hpBarOffset = new Vector3(0f, -4f, 0f);
    
    private AnimalHPBar hpBarInstance;

    // 「Nターンに1回しか動けない」動物用のカウンタ
    private int turnCounter = 0;

    // 威嚇成功時、次のOnTurnTickで1回だけ移動をスキップする。
    private bool skipMoveThisTurn = false;

    // 沼地に入った直後、次ターンのスキップを確定させる。
    private bool skipNextTurnBySwamp = false;

    // 1回の行動中に沼地へ入ったら、その時点で移動を打ち切る。
    private bool caughtBySwampDuringMove = false;

    // <summary>
    // HP これまでに受けた捕獲判定の回数。
    // </summary>
    private int captureHits = 0;
    
    /// <summary>
    /// 捕獲判定を1回受ける（既存API）。
    /// </summary>
    public bool RegisterCaptureHit()
    {
        return ApplyCaptureDamage(1);
    }

    /// <summary>
    /// 捕獲HPに対してダメージを与える。
    /// 戻り値: 捕獲条件（HP 0以下）に到達したか。
    /// </summary>
    public bool ApplyCaptureDamage(int damage)
    {
        int actualDamage = Mathf.Max(0, damage);
        if (actualDamage <= 0) return false;

        captureHits += actualDamage;
        int required = Mathf.Max(1, stats.captureHitsRequired);
        int remaining = Mathf.Max(0, required - captureHits);

        Debug.Log($"HPバー更新確認: {stats.animalName}, 捕獲判定={captureHits}/{required}, HPバー={(hpBarInstance != null ? "あり" : "なし")}");

        if (hpBarInstance != null)
        {
            hpBarInstance.UpdateHP(captureHits, required);
        }

        Debug.Log($"{stats.animalName}: ダメージ {actualDamage} / 残HP {remaining}");
        return captureHits >= required;
    }

    private void Start()
    {
        statusEffectUI = GetComponentInChildren<AnimalStatusEffectUI>();

        //HPバーの生成
        if (hpBarPrefab != null)
        {
            hpBarInstance = Instantiate(hpBarPrefab, transform);
            hpBarInstance.transform.localPosition = hpBarOffset;
            hpBarInstance.transform.localRotation = Quaternion.identity;
            hpBarInstance.transform.localScale = new Vector3(0.08f, 0.1f, 0.1f);
            
            hpBarInstance.UpdateHP(captureHits, stats.captureHitsRequired);
        }

        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            Debug.LogError($"{name}: FieldGridConfigが見つかりません。シーンに配置しGridを設定してください。", this);
            return;
        }

        CurrentCell = FieldGridConfig.Instance.grid.WorldToCell(transform.position);
        AnimalOccupancyMap.Instance.SetOccupied(CurrentCell, this);
        TurnManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
        if (AnimalOccupancyMap.Instance != null) AnimalOccupancyMap.Instance.ClearOccupied(CurrentCell, this);
    }

    /// <summary>
    /// 妨害効果(沼地・鎖など)から呼ばれ、行動不能ターンを積み増す。
    /// </summary>
    public void AddMoveDelay(int turns)
    {
        if (turns <= 0) return;
        moveDelayTurns += turns;
    }

    public void ShowSweatEffect()
    {
        if (statusEffectUI != null)
        {
            statusEffectUI.ShowSweat();
        }
    }

    public void ShowHeartEffect()
    {
        if (statusEffectUI != null)
        {
            statusEffectUI.ShowHeart();
        }
    }

    /// <summary>
    /// 威嚇直後の1ターンだけ移動を禁止する。
    /// </summary>
    public void ApplyIntimidationForNextTurn()
    {
        skipMoveThisTurn = true;
    }

    /// <summary>
    /// 沼地に入ったときの共通処理。
    /// 「次ターンの行動スキップ」を確定させ、同ターンの追加移動を停止する。
    /// </summary>
    public void ApplySwampCatchOnEnter()
    {
        if (stats.swampImmune) return;

        skipNextTurnBySwamp = true;
        caughtBySwampDuringMove = true;
        ShowSweatEffect();
    }

    public void OnTurnTick()
    {
        if (skipMoveThisTurn)
        {
            skipMoveThisTurn = false;
            return;
        }

        if (skipNextTurnBySwamp)
        {
            skipNextTurnBySwamp = false;
            if (moveDelayTurns > 0) moveDelayTurns--;
            return;
        }

        if (moveDelayTurns > 0)
        {
            moveDelayTurns--;
            return;
        }

        turnCounter++;
        if (turnCounter < stats.turnsPerMove) return;
        turnCounter = 0;

        Move();
    }

    public bool TryGetPlannedPath(out List<Vector3Int> path)
    {
        path = null;

        if (!TryBuildGoals(out var attractiveCells, out var goals)) return false;
        if (attractiveCells.Contains(CurrentCell)) return false;

        path = FindPathFrom(CurrentCell, goals);
        return path != null && path.Count >= 2;
    }

    public bool TryGetNextTurnDestination(out Vector3Int destination)
    {
        destination = default;

        if (moveDelayTurns > 0) return false;
        if (!WillAttemptMoveOnNextTurn()) return false;
        if (!TryBuildGoals(out var attractiveCells, out var goals)) return false;
        if (attractiveCells.Contains(CurrentCell)) return false;

        Vector3Int simulatedCell = CurrentCell;
        bool moved = false;
        int maxSteps = Mathf.Max(0, stats.squaresPerTurn);

        for (int i = 0; i < maxSteps; i++)
        {
            if (IsAtGoal(simulatedCell)) break;

            var path = FindPathFrom(simulatedCell, goals);
            if (path == null || path.Count < 2) break;

            simulatedCell = path[1];
            moved = true;

            if (attractiveCells.Contains(simulatedCell)) break;
        }

        if (!moved) return false;

        destination = simulatedCell;
        return true;
    }

    /// <summary>
    /// 現在の進行方向と逆向きの1マスを返す。
    /// 進行方向は「現在地からゴールへ向かう経路の1歩目」で判定する。
    /// </summary>
    public bool TryGetRetreatCell(out Vector3Int retreatCell)
    {
        retreatCell = default;

        if (FieldGridConfig.Instance == null ||
            AnimalOccupancyMap.Instance == null)
        {
            return false;
        }

        if (!TryBuildGoals(out _, out var goals)) return false;

        // 威嚇用の進行方向判定では、他動物の一時的な詰まりを無視して
        // 「本来の進行方向」を求める。
        var path = AStarPathfinder.FindPath(
            CurrentCell,
            goals,
            FieldGridConfig.Instance.IsWalkable,
            cell => false,
            CostAt);

        Vector3Int forward;
        if (path != null && path.Count >= 2)
        {
            forward = path[1] - CurrentCell;
        }
        else if (!TryGetGoalDirectionFallback(goals, out forward))
        {
            return false;
        }

        if (forward == Vector3Int.zero) return false;

        retreatCell = CurrentCell - forward;

        if (!FieldGridConfig.Instance.IsWalkable(retreatCell)) return false;
        if (AnimalOccupancyMap.Instance.IsOccupied(retreatCell)) return false;

        return true;
    }

    private bool TryGetGoalDirectionFallback(IReadOnlyList<Vector3Int> goals, out Vector3Int direction)
    {
        direction = Vector3Int.zero;
        if (goals == null || goals.Count == 0) return false;

        int bestDistance = int.MaxValue;
        Vector3Int nearestGoal = goals[0];
        foreach (var goal in goals)
        {
            int d = Mathf.Abs(goal.x - CurrentCell.x) + Mathf.Abs(goal.y - CurrentCell.y);
            if (d < bestDistance)
            {
                bestDistance = d;
                nearestGoal = goal;
            }
        }

        int dx = nearestGoal.x - CurrentCell.x;
        int dy = nearestGoal.y - CurrentCell.y;

        if (Mathf.Abs(dx) >= Mathf.Abs(dy) && dx != 0)
        {
            direction = new Vector3Int(dx > 0 ? 1 : -1, 0, 0);
            return true;
        }

        if (dy != 0)
        {
            direction = new Vector3Int(0, dy > 0 ? 1 : -1, 0);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 動物をゴールから遠ざかる方向に1マス後退させる。
    /// </summary>
    public bool TryRetreatOneCell()
    {
        if (!TryGetRetreatCell(out var retreatCell)) return false;
        return TryMoveToCell(retreatCell);
    }

    /// <summary>
    /// 指定セルへ即時移動する。占有情報と見た目の位置を既存処理で更新する。
    /// </summary>
    public bool TryMoveToCell(Vector3Int targetCell)
    {
        if (FieldGridConfig.Instance == null ||
            AnimalOccupancyMap.Instance == null)
        {
            return false;
        }

        if (!FieldGridConfig.Instance.IsWalkable(targetCell)) return false;
        if (AnimalOccupancyMap.Instance.IsOccupiedByOther(targetCell, this)) return false;
        if (targetCell == CurrentCell) return false;

        EnterCell(targetCell);
        return true;
    }

    private void Move()
    {
        if (!TryBuildGoals(out var attractiveCells, out var goals)) return;

        caughtBySwampDuringMove = false;

        // 現在いるマスが誘引マスなら移動せず待機する(食べている最中)
        if (attractiveCells.Contains(CurrentCell)) return;

        for (int i = 0; i < stats.squaresPerTurn; i++)
        {
            if (IsAtGoal(CurrentCell))
            {
                TryExit();
                return;
            }

            var path = AStarPathfinder.FindPath(
                CurrentCell,
                goals,
                FieldGridConfig.Instance.IsWalkable,
                cell => AnimalOccupancyMap.Instance.IsOccupiedByOther(cell, this),
                CostAt);

            // 経路が見つからない(他の動物や妨害効果で完全に塞がれている)場合はこのターン待機
            if (path == null || path.Count < 2) return;

            EnterCell(path[1]);

            // 3マス移動などでも、途中で沼地に入ったらその時点で強制停止する。
            if (caughtBySwampDuringMove)
            {
                return;
            }

            // 誘引マスに到達したらそのマスで移動を止める
            if (attractiveCells.Contains(CurrentCell))
            {
                ShowHeartEffect();
                return;
            }
        }

        if (IsAtGoal(CurrentCell))
        {
            TryExit();
        }
    }

    private bool TryBuildGoals(out List<Vector3Int> attractiveCells, out List<Vector3Int> goals)
    {
        attractiveCells = FieldEffectMap.Instance != null
            ? FieldEffectMap.Instance.GetAttractiveCells(this)
            : new List<Vector3Int>();

        goals = new List<Vector3Int>(attractiveCells);

        if (FieldGridConfig.Instance == null)
        {
            return goals.Count > 0;
        }

        goals.AddRange(FieldGridConfig.Instance.goalCells);
        return goals.Count > 0;
    }

    private List<Vector3Int> FindPathFrom(Vector3Int startCell, IReadOnlyList<Vector3Int> goals)
    {
        if (FieldGridConfig.Instance == null) return null;
        if (AnimalOccupancyMap.Instance == null) return null;

        return AStarPathfinder.FindPath(
            startCell,
            goals,
            FieldGridConfig.Instance.IsWalkable,
            cell => AnimalOccupancyMap.Instance.IsOccupiedByOther(cell, this),
            CostAt);
    }

    private bool WillAttemptMoveOnNextTurn()
    {
        int turnsPerMove = Mathf.Max(1, stats.turnsPerMove);
        return turnCounter + 1 >= turnsPerMove;
    }

    private bool IsAtGoal(Vector3Int cell)
    {
        return FieldGridConfig.Instance != null && FieldGridConfig.Instance.goalCells.Contains(cell);
    }

    private float CostAt(Vector3Int cell)
    {
        if (FieldEffectMap.Instance != null &&
            FieldEffectMap.Instance.TryGetEffect(cell, out IFieldEffect effect))
        {
            return effect.GetPathCost(this);
        }
        return 1f;
    }

    private void EnterCell(Vector3Int cell)
    {
        AnimalOccupancyMap.Instance.ClearOccupied(CurrentCell, this);
        CurrentCell = cell;
        transform.position = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        AnimalOccupancyMap.Instance.SetOccupied(cell, this);

        // ゴールマスの効果はTryExit側で処理するので、通過中のマスのみここで適用する
        if (!IsAtGoal(cell) &&
            FieldEffectMap.Instance != null &&
            FieldEffectMap.Instance.TryGetEffect(cell, out IFieldEffect effect))
        {
            effect.OnAnimalEnter(this);
        }
    }

    private void TryExit()
    {
        if (FieldEffectMap.Instance != null &&
            FieldEffectMap.Instance.TryGetEffect(CurrentCell, out IFieldEffect effect))
        {
            bool passed = effect.OnAnimalEnter(this);
            if (!passed) return; // 鎖などで足止めされ、脱出は成立しない
        }

        Debug.Log($"{stats.animalName} が脱走した!");
        // GameManager側の「脱走数カウント」加算処理をここから呼ぶ
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.AddEscape();
        }
        Destroy(gameObject);
    }
}
