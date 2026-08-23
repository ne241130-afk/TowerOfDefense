using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// wave・ターン指定スケジュール用エントリー。
/// 特定の wave の特定ターンに1体スポーンさせる。
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
/// 一定ターン間隔でランダムにスポーンするグループ。
/// プレハブと座標をそれぞれリストで登録し、毎回ランダムに選択する。
/// </summary>
[System.Serializable]
public class RandomSpawnGroup
{
    [Tooltip("発動する wave 番号。0 以下なら全 wave で発動。")]
    public int wave = 0;

    [Tooltip("何ターンごとに1体生成するか。")]
    public int turnsPerSpawn = 3;

    [Tooltip("生成する動物プレハブの候補。毎回ランダムに1体選ばれる。")]
    public List<GameObject> animalPrefabs = new List<GameObject>();

    [Tooltip(
        "スポーン地点の候補セル座標。複数登録するとランダムに1か所選ばれる。\n" +
        "空の場合は AnimalSpawner の defaultSpawnCell を使用する。")]
    public List<Vector3Int> spawnCells = new List<Vector3Int>();

    /// <summary>内部ターンカウンタ。シリアライズしないことでPlayMode再生時に0スタートになる。</summary>
    [System.NonSerialized] public int turnCounter = 0;
}

/// <summary>
/// wave・ターン指定スケジュール型 ＋ ランダムインターバル型 の動物スポナー。
///
/// ■ schedule（特定ターン指定）
///   - 指定した wave + ターンに特定の動物を1体スポーンする
///   - wave=0 以下なら全 wave 共通
///
/// ■ randomGroups（ランダムインターバル）
///   - N ターンごとにプレハブリストからランダム選択してスポーン
///   - spawnCells リストを登録すると座標もランダムに選ばれる
///   - どちらも空の場合は defaultSpawnCell + 先頭プレハブにフォールバック
/// </summary>
public class AnimalSpawner : MonoBehaviour, ITurnActor
{
    [Header("デフォルトスポーン地点")]
    [Tooltip("SpawnEntry で useDefaultSpawnCell = true、または RandomSpawnGroup の spawnCells が空のときに使用されるセル座標。")]
    public Vector3Int defaultSpawnCell = new Vector3Int(0, -16, 0);

    [Header("スポーンスケジュール（特定ターン指定）")]
    [Tooltip("wave・ターンごとの生成設定。特定のタイミングに特定の動物を出すときに使う。")]
    public List<SpawnEntry> schedule = new List<SpawnEntry>();

    [Header("ランダムスポーングループ（インターバル型）")]
    [Tooltip("N ターンごとにプレハブ・座標をランダムで選んでスポーンするグループ。複数グループを登録可能。")]
    public List<RandomSpawnGroup> randomGroups = new List<RandomSpawnGroup>();

    // WaveManager に依存せず独自にターン数を管理する（登録順序に影響されない）
    private int myTurnInWave = 0;
    private int myCurrentWave = 1;

    // schedule 用リトライ（セルが占有中だった場合）
    private readonly List<SpawnEntry> pendingRetries = new List<SpawnEntry>();

    // randomGroups 用リトライ（セルが占有中だった場合）
    private struct PendingRandomSpawn { public GameObject prefab; public Vector3Int cell; }
    private readonly List<PendingRandomSpawn> pendingRandomRetries = new List<PendingRandomSpawn>();

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
        pendingRandomRetries.Clear();

        foreach (var group in randomGroups)
            group.turnCounter = 0;
    }

    public void OnTurnTick()
    {
        myTurnInWave++;

        // ── schedule のリトライ ──
        var retrySnapshot = new List<SpawnEntry>(pendingRetries);
        pendingRetries.Clear();
        foreach (var entry in retrySnapshot)
        {
            if (!TrySpawnEntry(entry))
                pendingRetries.Add(entry);
        }

        // ── schedule の今ターン発動エントリー ──
        foreach (var entry in schedule)
        {
            if (entry.animalPrefab == null) continue;

            bool waveMatch = entry.wave <= 0 || entry.wave == myCurrentWave;
            if (!waveMatch) continue;
            if (entry.turnInWave != myTurnInWave) continue;

            if (!TrySpawnEntry(entry))
                pendingRetries.Add(entry);
        }

        // ── randomGroups のリトライ ──
        var randomRetrySnapshot = new List<PendingRandomSpawn>(pendingRandomRetries);
        pendingRandomRetries.Clear();
        foreach (var pending in randomRetrySnapshot)
        {
            if (!TrySpawnAt(pending.prefab, pending.cell))
                pendingRandomRetries.Add(pending);
        }

        // ── randomGroups のインターバル処理 ──
        foreach (var group in randomGroups)
        {
            if (group.animalPrefabs == null || group.animalPrefabs.Count == 0) continue;

            bool waveMatch = group.wave <= 0 || group.wave == myCurrentWave;
            if (!waveMatch) continue;

            group.turnCounter++;
            if (group.turnCounter < group.turnsPerSpawn) continue;
            group.turnCounter = 0;

            // プレハブをランダム選択
            GameObject prefab = group.animalPrefabs[Random.Range(0, group.animalPrefabs.Count)];

            // 座標をランダム選択（リストが空なら defaultSpawnCell）
            Vector3Int cell = (group.spawnCells != null && group.spawnCells.Count > 0)
                ? group.spawnCells[Random.Range(0, group.spawnCells.Count)]
                : defaultSpawnCell;

            if (!TrySpawnAt(prefab, cell))
                pendingRandomRetries.Add(new PendingRandomSpawn { prefab = prefab, cell = cell });
        }
    }

    /// <summary>SpawnEntry に基づいて動物をスポーンする。</summary>
    private bool TrySpawnEntry(SpawnEntry entry)
    {
        Vector3Int cell = entry.useDefaultSpawnCell ? defaultSpawnCell : entry.spawnCell;
        return TrySpawnAt(entry.animalPrefab, cell);
    }

    /// <summary>
    /// 指定プレハブを指定セルにスポーンする共通処理。
    /// セルが占有中なら false を返す（呼び出し元がリトライを管理する）。
    /// </summary>
    private bool TrySpawnAt(GameObject prefab, Vector3Int cell)
    {
        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            Debug.LogWarning("AnimalSpawner: FieldGridConfig が見つかりません。");
            return false;
        }

        if (AnimalOccupancyMap.Instance != null && AnimalOccupancyMap.Instance.IsOccupied(cell))
            return false; // セルが占有中 → 次ターンにリトライ

        Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);

        // AnimalController.Start() より前に占有を登録し、同ターン内の経路探索干渉を防ぐ
        var controller = obj.GetComponent<AnimalController>();
        if (controller != null && AnimalOccupancyMap.Instance != null)
            AnimalOccupancyMap.Instance.SetOccupied(cell, controller);

        Debug.Log($"[AnimalSpawner] Wave{myCurrentWave} Turn{myTurnInWave}: {prefab.name} をスポーン (cell: {cell})");
        return true;
    }
}
