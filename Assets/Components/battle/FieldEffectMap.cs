using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// フィールド上に設置される妨害効果の共通インターフェース。
/// 沼地・鎖(鍵)・肉(肉食動物の足止め)などはこれを実装する。
/// </summary>
public interface IFieldEffect
{
    /// <summary>
    /// 動物がこのマスに侵入(通過/脱出)した際に呼ばれる。
    /// 戻り値は「この侵入が成功したか」。
    /// 鎖のように出口自体を塞ぐ場合はfalseを返して足止めする。
    /// </summary>
    bool OnAnimalEnter(AnimalController animal);

    /// <summary>
    /// 経路探索(A*)でこのマスを通る際のコスト。
    /// 1 = 通常のマスと同じ。大きいほど迂回されやすくなる。
    /// float.PositiveInfinity を返すと完全に通行不可扱いになる。
    /// </summary>
    float GetPathCost(AnimalController animal);
}

/// <summary>
/// セル座標(Vector3Int) → 妨害効果 の対応を管理するシングルトン。
/// 既存のGrid/Tilemapとは別に、論理的な「効果レイヤー」として持つ。
/// </summary>
public class FieldEffectMap : MonoBehaviour
{
    public static FieldEffectMap Instance { get; private set; }

    private readonly Dictionary<Vector3Int, IFieldEffect> effects = new Dictionary<Vector3Int, IFieldEffect>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetEffect(Vector3Int cell, IFieldEffect effect)
    {
        effects[cell] = effect;
    }

    public void RemoveEffect(Vector3Int cell)
    {
        effects.Remove(cell);
    }

    public bool TryGetEffect(Vector3Int cell, out IFieldEffect effect)
    {
        return effects.TryGetValue(cell, out effect);
    }
}
