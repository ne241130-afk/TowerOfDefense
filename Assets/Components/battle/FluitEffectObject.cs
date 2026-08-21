using UnityEngine;

/// <summary>
/// 肉のビジュアル兼ターン管理コンポーネント。
/// 肉カードの placedVisualPrefab となるプレハブにアタッチして使う。
///
/// Unity はファイル名とクラス名が一致しないと MonoBehaviour として認識されないため、
/// MeatFieldEffect.cs とは別ファイルに定義する。
///
/// - 配置されたセルを自動検出し TurnManager に登録する
/// - 毎ターン: 同じセルに肉食動物がいれば occupiedTurns をカウントアップ
/// - occupiedTurns が despawnTurns に達したら FieldEffectMap から除去して自己破棄する
/// </summary>
public class FluitEffectObject : MonoBehaviour, ITurnActor
{
    /// <summary>何ターン肉食動物が留まると消滅するか。</summary>
    public int despawnTurns = 3;

    private Vector3Int myCell;
    private int occupiedTurns = 0;

    private void Start()
    {
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            Debug.LogWarning("FluitEffectObject: FieldGridConfig が見つかりません。", this);
            return;
        }

        myCell = FieldGridConfig.Instance.grid.WorldToCell(transform.position);
        TurnManager.Instance?.Register(this);
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
        if (FieldEffectMap.Instance != null) FieldEffectMap.Instance.RemoveEffect(myCell);
    }

    /// <summary>
    /// 毎ターン呼ばれる。同じセルに肉食動物がいるターン数を数え、
    /// despawnTurns に達したら自己破棄する。
    /// </summary>
    public void OnTurnTick()
    {
        if (AnimalOccupancyMap.Instance == null) return;
        if (!AnimalOccupancyMap.Instance.TryGetAnimalAt(myCell, out var animal)) return;
        if (animal.Stats.diet != AnimalDiet.Herbivore) return;

        occupiedTurns++;
        if (occupiedTurns >= despawnTurns)
        {
            Destroy(gameObject);
        }
    }
}
