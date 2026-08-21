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

    // 「Nターンに1回しか動けない」動物用のカウンタ
    private int turnCounter = 0;

    private void Start()
    {
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

    public void OnTurnTick()
    {
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

    private void Move()
    {
        // 誘引マスを取得(肉食動物は肉のマスなど)
        List<Vector3Int> attractiveCells = FieldEffectMap.Instance != null
            ? FieldEffectMap.Instance.GetAttractiveCells(this)
            : new List<Vector3Int>();

        // 現在いるマスが誘引マスなら移動せず待機する(食べている最中)
        if (attractiveCells.Contains(CurrentCell)) return;

        // 誘引マス(優先) + ゴールマスを合わせた目標リストでA*を実行
        var goals = new List<Vector3Int>(attractiveCells);
        goals.AddRange(FieldGridConfig.Instance.goalCells);

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
        // TODO: GameManager側の「脱走数カウント」加算処理をここから呼ぶ
        Destroy(gameObject);
    }
}
