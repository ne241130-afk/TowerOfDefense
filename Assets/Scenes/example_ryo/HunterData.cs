using UnityEngine;

/// <summary>
/// ハンターの雇用形態。
/// 時給制: 一定間隔ごとに費用を消費し続ける代わりに使用回数の制限は無い
/// 購入制: 購入時に一度だけ費用を払えばそのゲーム中ずっと稼働する代わりに、
///        wave数に応じた設置数制限がかかる想定(制限自体は今後の配置システム側で実装)
/// </summary>
public enum HunterHireType
{
    HourlyWage, // 時給制
    Purchase,   // 購入制
}

/// <summary>
/// ハンター1体分のデータ。
/// </summary>
[System.Serializable]
public class HunterData
{
    [Header("基本情報")]
    public string hunterName = "ハンターA";
    [TextArea] public string description = "";

    [Header("雇用形態")]
    public HunterHireType hireType = HunterHireType.HourlyWage;

    [Tooltip("時給制: 支払い間隔ごとに消費する金額 / 購入制: 雇用時に一度だけ消費する金額。所持金システム導入時に接続予定(現状は未使用)")]
    public int cost = 100;

    [Tooltip("時給制のみ使用: 何ターンごとに費用を消費するか")]
    public int paymentIntervalTurns = 60;

    [Header("移動・検知性能")]
    [Tooltip("1ターンに移動できるマス数")]
    public int squaresPerTurn = 1;

    [Tooltip("動物を検知する範囲(Chebyshev距離)。0以下なら距離無制限")]
    public int detectionRange = 5;
}
