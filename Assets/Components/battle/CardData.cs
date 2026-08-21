using UnityEngine;

/// <summary>
/// カードが持つ効果の種類。既存のIFieldEffect実装と対応させる。
/// 今後、肉・果物盛り合わせ・捕獲ネットランチャーなどを増やす際はここに追加していく。
/// </summary>
public enum CardEffectType
{
    Swamp,          // 沼地
    ChainLock,      // 鎖(鍵)
    SummonHunter,   // ハンター召喚
    Meat,           // 肉(肉食動物を誘引)
    Fluit,          // 果物(草食動物を誘引)
}

/// <summary>
/// カード1枚分のデータ。Projectウィンドウでアセットとして作成し、
/// CardDeckManagerのdeckリストにドラッグして使う。
/// 右クリック → Create → TowerDefense → CardData で作成できる。
/// </summary>
[CreateAssetMenu(fileName = "NewCardData", menuName = "TowerDefense/CardData")]
public class CardData : ScriptableObject
{
    [Header("基本情報")]
    public string cardName = "沼地";
    [TextArea] public string description = "エリア内に沼を生成する。沼にいる動物の移動を遅らせる。";
    public Sprite icon;

    [Header("効果")]
    public CardEffectType effectType = CardEffectType.Swamp;

    [Tooltip("設置範囲。0=中心の1マスのみ, 1=3x3, 2=5x5 ...")]
    public int areaRadius = 0;

    [Header("見た目")]
    [Tooltip("フィールドに設置されたときに表示するプレハブ。未設定ならCardPlacementControllerのデフォルトを使う")]
    public GameObject placedVisualPrefab;

    [Header("コスト(所持金システム導入時に使用予定。現状は未使用)")]
    public int cost = 50;

    [Header("ハンター召喚(effectType == SummonHunter のときのみ使用)")]
    [Tooltip("召喚するハンターのプレハブ。HunterControllerコンポーネントがアタッチされている必要がある")]
    public GameObject hunterPrefab;
}
