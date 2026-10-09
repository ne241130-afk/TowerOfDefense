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
    public bool HasInitializedCell { get; private set; }

    // 足止め残りターン数(沼地・鎖などから加算される)
    private int moveDelayTurns = 0;

    // 「Nターンに1回しか動けない」動物用のカウンタ
    private int turnCounter = 0;
    private bool removedFromBoard = false;

    private void Start()
    {
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            Debug.LogError($"{name}: FieldGridConfigが見つかりません。シーンに配置しGridを設定してください。", this);
            return;
        }

        if (!HasInitializedCell)
        {
            CurrentCell = FieldGridConfig.Instance.grid.WorldToCell(transform.position);
            HasInitializedCell = true;
        }

        AnimalOccupancyMap.Instance.SetOccupied(CurrentCell, this);
        TurnManager.Instance.Register(this);
        AnimalPathVisualizer.RequestRefresh();
    }

    private void OnDestroy()
    {
        RemoveFromBoard();
        AnimalPathVisualizer.RequestRefresh();
    }

    public void InitializeAtCell(Vector3Int cell)
    {
        CurrentCell = cell;
        HasInitializedCell = true;
    }

    public void RemoveFromBoard()
    {
        if (removedFromBoard) return;

        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
        if (AnimalOccupancyMap.Instance != null) AnimalOccupancyMap.Instance.ClearOccupied(CurrentCell, this);

        removedFromBoard = true;
    }

    /// <summary>
    /// 妨害効果(沼地・鎖など)から呼ばれ、行動不能ターンを積み増す。
    /// </summary>
    public void AddMoveDelay(int turns)
    {
        if (turns <= 0) return;
        moveDelayTurns += turns;
        AnimalPathVisualizer.RequestRefresh();
    }

    public void OnTurnTick()
    {
        if (moveDelayTurns > 0)
        {
            moveDelayTurns--;
            return;
        }

        turnCounter++;
        if (turnCounter < Mathf.Max(1, stats.turnsPerMove)) return;
        turnCounter = 0;

        Move();
    }

    public bool TryGetPlannedPath(out List<Vector3Int> path)
    {
        path = null;

        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null || !HasInitializedCell)
        {
            return false;
        }

        List<Vector3Int> attractiveCells = GetAttractiveCells();
        if (attractiveCells.Contains(CurrentCell)) return false;

        return TryFindPath(BuildGoals(attractiveCells), out path);
    }

    public bool TryGetNextMovePreview(out Vector3Int nextCell)
    {
        nextCell = default;

        if (!WillMoveOnNextTurn()) return false;
        if (!TryGetPlannedPath(out List<Vector3Int> path)) return false;

        nextCell = path[1];
        return true;
    }

    private void Move()
    {
        List<Vector3Int> attractiveCells = GetAttractiveCells();

        // 現在いるマスが誘引マスなら移動せず待機する(食べている最中)
        if (attractiveCells.Contains(CurrentCell)) return;

        // 誘引マス(優先) + ゴールマスを合わせた目標リストでA*を実行
        List<Vector3Int> goals = BuildGoals(attractiveCells);

        for (int i = 0; i < stats.squaresPerTurn; i++)
        {
            if (IsAtGoal(CurrentCell))
            {
                TryExit();
                return;
            }

            if (!TryFindPath(goals, out List<Vector3Int> path)) return;

            EnterCell(path[1]);

            // 誘引マスに到達したらそのマスで移動を止める
            if (attractiveCells.Contains(CurrentCell)) return;
        }

        if (IsAtGoal(CurrentCell))
        {
            TryExit();
        }
    }

    private bool IsAtGoal(Vector3Int cell)
    {
        return FieldGridConfig.Instance.goalCells.Contains(cell);
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
        AnimalPathVisualizer.RequestRefresh();

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
        RemoveFromBoard();
        AnimalPathVisualizer.RequestRefresh();
        Destroy(gameObject);
    }

    private List<Vector3Int> GetAttractiveCells()
    {
        return FieldEffectMap.Instance != null
            ? FieldEffectMap.Instance.GetAttractiveCells(this)
            : new List<Vector3Int>();
    }

    private List<Vector3Int> BuildGoals(List<Vector3Int> attractiveCells)
    {
        var goals = new List<Vector3Int>(attractiveCells);
        goals.AddRange(FieldGridConfig.Instance.goalCells);
        return goals;
    }

    private bool TryFindPath(IReadOnlyList<Vector3Int> goals, out List<Vector3Int> path)
    {
        path = AStarPathfinder.FindPath(
            CurrentCell,
            goals,
            FieldGridConfig.Instance.IsWalkable,
            cell => AnimalOccupancyMap.Instance.IsOccupiedByOther(cell, this),
            CostAt);

        return path != null && path.Count >= 2;
    }

    private bool WillMoveOnNextTurn()
    {
        if (moveDelayTurns > 0) return false;
        return turnCounter + 1 >= Mathf.Max(1, stats.turnsPerMove);
    }
}
