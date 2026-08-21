/// <summary>
/// CardEffectTypeから実際のIFieldEffectインスタンスを生成するファクトリ。
/// 新しいカード種類を増やしたら、対応するIFieldEffect実装とここへの分岐を追加する。
/// </summary>
public static class CardEffectFactory
{
    public static IFieldEffect CreateEffect(CardEffectType type)
    {
        switch (type)
        {
            case CardEffectType.Swamp:
                return new SwampFieldEffect();
            case CardEffectType.ChainLock:
                return new ChainLockFieldEffect();
            case CardEffectType.SummonHunter:
                // ハンター召喚はIFieldEffectではなくCardPlacementController側で処理するため null を返す
                return null;
            case CardEffectType.Meat:
                return new MeatFieldEffect();
            default:
                return null;
        }
    }
}
