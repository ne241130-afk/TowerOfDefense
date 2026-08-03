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
public class ChainLockFieldEffect : IFieldEffect
{
    private const int baseLockTurns = 3;

    public bool OnAnimalEnter(AnimalController animal)
    {
        int lockTurns = Mathf.Max(0, baseLockTurns - animal.Stats.lockTurnReduction);

        if (lockTurns > 0)
        {
            animal.AddMoveDelay(lockTurns);
            return false;
        }

        return true;
    }

    public float GetPathCost(AnimalController animal)
    {
        int lockTurns = Mathf.Max(0, baseLockTurns - animal.Stats.lockTurnReduction);
        if (lockTurns <= 0) return 1f;

        return 1f + lockTurns * 5f;
    }
}
