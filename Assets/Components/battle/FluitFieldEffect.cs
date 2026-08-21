/// <summary>
/// 果物のフィールドエフェクト。
/// 草食(Herbivore)の動物を優先的に引き寄せる。
///
/// セットアップ:
///   CardData の effectType を Fluit に設定し、
///   placedVisualPrefab に FluitEffectObject コンポーネントを持つプレハブを設定する。
///   (FluitEffectObject は FluitEffectObject.cs で定義)
/// </summary>
public class FluitFieldEffect : IFieldEffect, IAttractiveEffect
{
    /// <summary>
    /// 誘引範囲(Chebyshev距離)。0 = 無制限。
    /// CardPlacementController が CardData.areaRadius から設定する。
    /// </summary>
    public int AttractionRange { get; set; } = 5;

    /// <summary>
    /// 動物がマスに侵入した際の処理。肉は移動阻害しないので常にtrueを返す。
    /// </summary>
    public bool OnAnimalEnter(AnimalController animal) => true;

    /// <summary>
    /// 経路探索コスト。肉食動物には低コスト(引き寄せ効果を補強)、それ以外は通常コスト。
    /// </summary>
    public float GetPathCost(AnimalController animal)
        => animal.Stats.diet == AnimalDiet.Herbivore ? 0.5f : 1f;

    /// <summary>
    /// 肉食動物のみを誘引する。
    /// </summary>
    public bool IsAttractive(AnimalController animal)
        => animal.Stats.diet == AnimalDiet.Herbivore;
}
