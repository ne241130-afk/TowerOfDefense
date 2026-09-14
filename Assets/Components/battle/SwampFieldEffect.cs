using UnityEngine;

/// <summary>
/// 沼地マスの効果。
/// 通行自体は可能だが、経路探索上のコストを上げることで
/// 「迂回できるなら迂回される」「迂回できなければ足止めを受けて通過する」の
/// 両方の挙動を1つの仕組みで表現する。
/// </summary>
public class SwampFieldEffect : IFieldEffect
{
    private const int baseExtraDelay = 1;

    public bool OnAnimalEnter(AnimalController animal)
    {
        if (animal.Stats.swampImmune) return true;

        int extraDelay = Mathf.RoundToInt(baseExtraDelay * animal.Stats.restrictionEffectMultiplier);
        animal.AddMoveDelay(extraDelay);
        return true;
    }

    public float GetPathCost(AnimalController animal)
    {
        if (animal.Stats.swampImmune) return 1f;

        float extraDelay = baseExtraDelay * animal.Stats.restrictionEffectMultiplier;
        // 足止めが重いほど迂回のインセンティブを強くする(重みは要バランス調整)
        return 1f + extraDelay * 3f;
    }
}

/// <summary>
/// 鎖(鍵)の効果。ゴールマスに設置し、動物が実際にそこへ到達したときのみ発動する。
/// 経路探索上はやや高コストにするだけなので、もう一方のゴールが空いていれば
/// そちらが自然に優先される(完全ブロックにはしない)。
/// </summary>

public class ChainLockFieldEffect : IFieldEffect, ITurnActor
{
    private const int baseLockTurns = 3;

    // 鎖が発動済みか
    private bool isActive = false;

    // 鎖が発動したターン
    private int startTurn = -1;
    
    // 鎖の画像
    private GameObject chainVisual;

    public ChainLockFieldEffect()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
        }
    }

     // 鎖の画像を登録する
    public void SetVisual(GameObject visual)
    {
        chainVisual = visual;
    }

    public bool OnAnimalEnter(AnimalController animal)
    {
        // まだ鎖が発動していない
        if (!isActive)
        {
            isActive = true;

            if (TurnManager.Instance != null)
            {
                startTurn = TurnManager.Instance.CurrentTurn;
            }

            int lockTurns = Mathf.Max(
                0,
                baseLockTurns - animal.Stats.lockTurnReduction
            );

            if (lockTurns > 0)
            {
                animal.AddMoveDelay(lockTurns);
                return false;
            }

            return true;
        }

        // 鎖が発動した後なら、
        // 3ターン以内か確認する
        if (TurnManager.Instance != null)
        {
            int elapsedTurns =
                TurnManager.Instance.CurrentTurn - startTurn;

            if (elapsedTurns < baseLockTurns)
            {
                int lockTurns = Mathf.Max(
                    0,
                    baseLockTurns - elapsedTurns
                );

                if (lockTurns > 0)
                {
                    animal.AddMoveDelay(lockTurns);
                    return false;
                }
            }
        }

        // 3ターン経過後
        return true;
    }

    public float GetPathCost(AnimalController animal)
    {
        // まだ発動していない間は通常のゴールとして扱う
        if (!isActive)
        {
            return 1f;
        }

        // 鎖が発動している間はゴールを高コストにする
        if (TurnManager.Instance != null)
        {
            int elapsedTurns =
                TurnManager.Instance.CurrentTurn - startTurn;

            if (elapsedTurns < baseLockTurns)
            {
                return 1f + baseLockTurns * 5f;
            }
        }

        return 1f;
    }

    public void OnTurnTick()
{
    if (!isActive) return;
    if (TurnManager.Instance == null) return;

    int elapsedTurns =
        TurnManager.Instance.CurrentTurn - startTurn;

    if (elapsedTurns >= baseLockTurns)
    {
        RemoveLock();
    }
}

private void RemoveLock()
{
    // ゴールの鎖効果を削除
    if (FieldGridConfig.Instance != null &&
        FieldEffectMap.Instance != null)
    {
        foreach (var goalCell in FieldGridConfig.Instance.goalCells)
        {
            if (FieldEffectMap.Instance.TryGetEffect(
                goalCell,
                out IFieldEffect effect))
            {
                if (ReferenceEquals(effect, this))
                {
                    FieldEffectMap.Instance.RemoveEffect(goalCell);
                }
            }
        }
    }

    // 鎖の画像を削除
        if (chainVisual != null)
        {
            Object.Destroy(chainVisual);
            chainVisual = null;
        }

    // TurnManagerから登録解除
    if (TurnManager.Instance != null)
    {
        TurnManager.Instance.Unregister(this);
    }

    isActive = false;
}

    }
