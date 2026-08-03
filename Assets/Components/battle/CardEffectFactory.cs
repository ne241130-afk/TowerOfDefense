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
            default:
                return null;
        }
    }
}
