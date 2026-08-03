using UnityEngine;

/// <summary>
/// 動物の食性。将来「肉食は肉に反応する」等の判定に使う。
/// </summary>
public enum AnimalDiet
{
    Carnivore, // 肉食
    Omnivore,  // 雑食
    Herbivore  // 草食
}

/// <summary>
/// 1体の動物が持つステータス。
/// ここの数値を変えるだけで、ライオン→チーター→カメ...と
/// 別の動物として振る舞うようにするのが狙い。
/// </summary>
[System.Serializable]
public class AnimalStats
{
    [Header("基本情報")]
    public string animalName = "Lion";
    public AnimalDiet diet = AnimalDiet.Carnivore;

    [Header("移動")]
    [Tooltip("1回の行動で移動できるマス数。チーターなら2など。")]
    public int squaresPerTurn = 1;

    [Tooltip("何ターンに1回行動できるか。カメ=3、ゾウ=2、それ以外は1。")]
    public int turnsPerMove = 1;

    [Header("妨害効果への耐性")]
    [Tooltip("沼地の足止め効果を完全無視できるか(ワシ・ワオキツネザル用)")]
    public bool swampImmune = false;

    [Tooltip("行動制限系アイテム(沼・罠など)の効果倍率。1=通常, 0.5=半減(カバ・ゾウ用)")]
    [Range(0f, 1f)]
    public float restrictionEffectMultiplier = 1f;

    [Tooltip("麻酔・睡眠系トラップの効果倍率(キリン=0.5想定)")]
    [Range(0f, 1f)]
    public float sleepEffectMultiplier = 1f;

    [Tooltip("鎖(鍵)による出口足止めターン数を何ターン軽減するか(ワオキツネザル=1)")]
    public int lockTurnReduction = 0;

    /// <summary>
    /// デバッグ用:ライオンの標準値(すべての効果を等倍で受ける基準キャラ)
    /// </summary>
    public static AnimalStats CreateLionDebug()
    {
        return new AnimalStats
        {
            animalName = "Lion",
            diet = AnimalDiet.Carnivore,
            squaresPerTurn = 1,
            turnsPerMove = 1,
            swampImmune = false,
            restrictionEffectMultiplier = 1f,
            sleepEffectMultiplier = 1f,
            lockTurnReduction = 0
        };
    }
}
