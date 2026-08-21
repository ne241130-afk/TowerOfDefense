using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// wave内の任意のターンに任意の動物をスポーンするエントリー。
/// AnimalSpawner の schedule リストに追加して使う。
/// </summary>
[System.Serializable]
public class SpawnEntry
{
    [Tooltip("発動する wave 番号。0 以下なら全 wave のこのターンで毎回発動。")]
    public int wave = 0;

    [Tooltip("wave 内の何ターン目に生成するか(1始まり)。")]
    public int turnInWave = 1;

    [Tooltip("生成する動物プレハブ。AnimalController がアタッチされている必要がある。")]
    public GameObject animalPrefab;

    [Tooltip("オンにすると AnimalSpawner の defaultSpawnCell を使用する。オフにすると下の spawnCell を使用する。")]
    public bool useDefaultSpawnCell = true;

    [Tooltip("useDefaultSpawnCell がオフのときに使うスポーン地点のセル座標。")]
    public Vector3Int spawnCell;
}

/// <summary>
/// wave・ターン指定スケジュール型の動物スポナー。
///
/// Inspector の schedule リストに SpawnEntry を追加することで、
/// 任意の wave・ターンに任意の動物をスポーンさせることができる。
///
/// セットアップ:
///   1. defaultSpawnCell にデフォルトのスポーン地点を設定する
///   2. schedule にエントリーを追加する
///      - wave: 0 以下なら全 wave 共通 / 1 以上なら指定 wave のみ
///      - turnInWave: wave 内の何ターン目か(1始まり)
///      - animalPrefab: 生成する動物プレハブ
///   3. スポーン地点が占有中の場合は次ターン以降に自動リトライする
/// </summary>
public class AnimalSpawner : MonoBehaviour, ITurnActor
{
    [Header("デフォルトスポーン地点")]
    [Tooltip("SpawnEntry で useDefaultSpawnCell = true の場合に使用されるセル座標。")]
    public Vector3Int defaultSpawnCell = new Vector3Int(0, -16, 0);

    [Header("スポーンスケジュール")]
    [Tooltip("wave・ターンごとの生成設定。エントリーを追加して自由に組み立てる。")]
    public List<SpawnEntry> schedule = new List<SpawnEntry>();

    // WaveManager に依存せず独自にターン数を管理することで登録順序に影響されない
    private int myTurnInWave = 0;
    private int myCurrentWave = 1;

    // スポーン地点が占有中で発動できなかったエントリーの再試行リスト
    private readonly List<SpawnEntry> pendingRetries = new List<SpawnEntry>();

    private void Start()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.Register(this);
        else
            Debug.LogError("AnimalSpawner: TurnManager が見つかりません。");

        if (WaveManager.Instance != null)
        {
            myCurrentWave = WaveManager.Instance.currentWave;
            WaveManager.Instance.OnWaveStarted.AddListener(OnWaveStarted);
        }
        else
        {
            Debug.LogWarning("AnimalSpawner: WaveManager が見つかりません。wave 指定は機能しません。");
        }
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
        if (WaveManager.Instance != null) WaveManager.Instance.OnWaveStarted.RemoveListener(OnWaveStarted);
    }

    /// <summary>wave 切り替わり時に呼ばれる。ターンカウンタとリトライをリセットする。</summary>
    private void OnWaveStarted(int wave)
    {
        myCurrentWave = wave;
        myTurnInWave = 0;
        pendingRetries.Clear();
    }

    public void OnTurnTick()
    {
        myTurnInWave++;

        // 前ターンで占有中だったエントリーを再試行
        var retrySnapshot = new List<SpawnEntry>(pendingRetries);
        pendingRetries.Clear();
        foreach (var entry in retrySnapshot)
        {
            if (!TrySpawnEntry(entry))
                pendingRetries.Add(entry);
        }

        // このターンに発動するスケジュールエントリーを処理
        foreach (var entry in schedule)
        {
            if (entry.animalPrefab == null) continue;

            bool waveMatch = entry.wave <= 0 || entry.wave == myCurrentWave;
            if (!waveMatch) continue;
            if (entry.turnInWave != myTurnInWave) continue;

            if (!TrySpawnEntry(entry))
                pendingRetries.Add(entry);
        }
    }

    /// <summary>
    /// エントリーに従って動物を1体生成する。
    /// スポーン地点が占有中の場合は false を返す(呼び出し元がリトライを管理する)。
    /// </summary>
    private bool TrySpawnEntry(SpawnEntry entry)
    {
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            Debug.LogWarning("AnimalSpawner: FieldGridConfig が見つかりません。");
            return false;
        }

        Vector3Int cell = entry.useDefaultSpawnCell ? defaultSpawnCell : entry.spawnCell;

        if (AnimalOccupancyMap.Instance != null && AnimalOccupancyMap.Instance.IsOccupied(cell))
            return false; // セルが占有中 → 次ターンにリトライ

        Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        GameObject obj = Instantiate(entry.animalPrefab, worldPos, Quaternion.identity);

        // AnimalController.Start() より前に占有を登録し、同ターン内の経路探索干渉を防ぐ
        var controller = obj.GetComponent<AnimalController>();
        if (controller != null && AnimalOccupancyMap.Instance != null)
            AnimalOccupancyMap.Instance.SetOccupied(cell, controller);

        Debug.Log($"[AnimalSpawner] Wave{myCurrentWave} Turn{myTurnInWave}: {entry.animalPrefab.name} をスポーン (cell: {cell})");
        return true;
    }
}
