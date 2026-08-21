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
/// 特定の動物を引き寄せる効果を持つフィールドエフェクトが実装するインターフェース。
/// IFieldEffect と合わせて実装し、FieldEffectMap.GetAttractiveCells で参照される。
/// </summary>
public interface IAttractiveEffect
{
    /// <summary>
    /// 指定した動物をこのマスへ誘引するかどうか。
    /// </summary>
    bool IsAttractive(AnimalController animal);

    /// <summary>
    /// 誘引が有効な距離(Chebyshev距離)。0 = 無制限。
    /// </summary>
    int AttractionRange { get; }
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

    /// <summary>
    /// 指定した動物を誘引するマスの一覧を返す。
    /// IAttractiveEffect を実装したエフェクトが設置されているセルのみが対象。
    /// AttractionRange > 0 の場合、動物の現在地からその距離内にあるセルのみ返す。
    /// </summary>
    public List<Vector3Int> GetAttractiveCells(AnimalController animal)
    {
        var result = new List<Vector3Int>();
        foreach (var pair in effects)
        {
            if (!(pair.Value is IAttractiveEffect attr)) continue;
            if (!attr.IsAttractive(animal)) continue;

            int range = attr.AttractionRange;
            if (range > 0 && ChebyshevDistance(animal.CurrentCell, pair.Key) > range) continue;

            result.Add(pair.Key);
        }
        return result;
    }

    private static int ChebyshevDistance(Vector3Int a, Vector3Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
