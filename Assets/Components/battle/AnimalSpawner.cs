using System.Collections.Generic;
using Mirror;
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

    [Tooltip("発動する最後のWave番号。0以下ならwaveと同じWaveだけで発動。waveが0以下の場合は無視される。")]
    public int waveEnd = 0;

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

    [Tooltip("スポーン地点周囲のカード配置禁止範囲 (Chebyshev 距離)。0 = 禁止なし、1 = 1マス以内を禁止。")]
    public int spawnProtectionRadius = 1;

    [Header("スポーンスケジュール（特定ターン指定）")]
    [Tooltip("wave・ターンごとの生成設定。特定のタイミングに特定の動物を出すときに使う。")]
    public List<SpawnEntry> schedule = new List<SpawnEntry>();

    [Header("ランダムスポーングループ（インターバル型）")]
    [Tooltip("N ターンごとにプレハブ・座標をランダムで選んでスポーンするグループ。複数グループを登録可能。")]
    public List<RandomSpawnGroup> randomGroups = new List<RandomSpawnGroup>();

    /// <summary>シーン内の全 AnimalSpawner インスタンス。カード配置制限の判定に使用する。</summary>
    public static readonly HashSet<AnimalSpawner> All = new HashSet<AnimalSpawner>();

    // WaveManager に依存せず独自にターン数を管理する（登録順序に影響されない）
    private int myTurnInWave = 0;
    private int myCurrentWave = 1;

    // schedule 用リトライ（セルが占有中だった場合）
    private readonly List<SpawnEntry> pendingRetries = new List<SpawnEntry>();

    // randomGroups 用リトライ（セルが占有中だった場合）
    private struct PendingRandomSpawn { public GameObject prefab; public Vector3Int cell; }
    private readonly List<PendingRandomSpawn> pendingRandomRetries = new List<PendingRandomSpawn>();
    private static int nextNetworkAnimalId = 1;

    private void Awake()
    {
        All.Add(this);
    }

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
        All.Remove(this);
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
        // In multiplayer, the server makes the random spawn decision once and
        // broadcasts the resulting prefab and cell to every client.
        if (MultiplayerRuntime.IsMultiplayerSession && !NetworkServer.active) return;

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

            //
            bool waveMatch;
            if (group.wave <= 0){ // waveが0以下なら全Waveで動作
             waveMatch = true;
            }
            else{ // waveEndが0以下なら、指定した1つのWaveだけで動作
             int endWave = group.waveEnd >= group.wave
             ? group.waveEnd
             : group.wave;
             waveMatch = myCurrentWave >= group.wave
             && myCurrentWave <= endWave;
            }
            if (!waveMatch) continue;

            group.turnCounter++;
            if (group.turnCounter < group.turnsPerSpawn) continue;
            group.turnCounter = 0;

            // プレハブをランダム選択
            GameObject prefab = group.animalPrefabs[Random.Range(0, group.animalPrefabs.Count)];

            //
            // 登録された全スポーン地点から1体ずつ出現させる。
            // スポーン地点が未登録なら、従来どおりdefaultSpawnCellを使う。
            if (group.spawnCells != null && group.spawnCells.Count > 0){
                foreach (Vector3Int cell in group.spawnCells){
                    if (!TrySpawnAt(prefab, cell)){
                        // セルが占有中なら、その地点だけ次ターンに再試行する
                        pendingRandomRetries.Add(
                            new PendingRandomSpawn{
                            prefab = prefab,
                            cell = cell
                        });
                    }
                }
            }else{
                if (!TrySpawnAt(prefab, defaultSpawnCell)){
                    pendingRandomRetries.Add(
                        new PendingRandomSpawn{
                            prefab = prefab,
                            cell = defaultSpawnCell
                        });
                }
            }
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

        int animalId = MultiplayerRuntime.IsMultiplayerSession && NetworkServer.active
            ? nextNetworkAnimalId++
            : 0;
        SpawnLocal(prefab, cell, animalId);

        if (MultiplayerRuntime.IsMultiplayerSession && NetworkServer.active)
        {
            NetworkServer.SendToAll(new AnimalSpawnMessage
            {
                animalId = animalId,
                prefabName = prefab.name,
                x = cell.x,
                y = cell.y,
                z = cell.z
            });
        }

        Debug.Log($"[AnimalSpawner] Wave{myCurrentWave} Turn{myTurnInWave}: {prefab.name} をスポーン (cell: {cell})");
        return true;
    }

    private void SpawnLocal(GameObject prefab, Vector3Int cell, int animalId)
    {
        Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(cell);
        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);

        // Register occupancy before AnimalController.Start() to prevent other
        // actors in this same tick from choosing the occupied cell.
        var controller = obj.GetComponent<AnimalController>();
        if (controller != null && AnimalOccupancyMap.Instance != null)
            AnimalOccupancyMap.Instance.SetOccupied(cell, controller);
        if (controller != null)
            controller.SetNetworkIdentity(animalId, prefab.name);
    }

    public static void ApplyNetworkSpawn(int animalId, string prefabName, Vector3Int cell)
    {
        if (string.IsNullOrEmpty(prefabName)) return;
        foreach (var spawner in All)
        {
            if (spawner == null) continue;
            GameObject prefab = spawner.ResolvePrefab(prefabName);
            if (prefab == null) continue;
            if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null) return;
            nextNetworkAnimalId = Mathf.Max(nextNetworkAnimalId, animalId + 1);
            spawner.SpawnLocal(prefab, cell, animalId);
            return;
        }
        Debug.LogWarning($"AnimalSpawner: received unknown animal prefab '{prefabName}'.");
    }

    public static string BuildAuthoritativeAnimalSnapshot()
    {
        var entries = new List<string>();
        foreach (var animal in FindObjectsByType<AnimalController>(FindObjectsSortMode.None))
        {
            if (animal == null || animal.NetworkAnimalId <= 0 || string.IsNullOrEmpty(animal.NetworkPrefabName)) continue;
            Vector3Int cell = animal.CurrentCell;
            entries.Add($"{animal.NetworkAnimalId},{animal.NetworkPrefabName},{cell.x},{cell.y},{cell.z},{animal.CaptureHits}");
        }
        return string.Join("|", entries);
    }

    public static void ApplyAuthoritativeAnimalSnapshot(string snapshot)
    {
        var records = new Dictionary<int, string[]>();
        if (!string.IsNullOrEmpty(snapshot))
        {
            foreach (string entry in snapshot.Split('|'))
            {
                string[] fields = entry.Split(',');
                if (fields.Length != 6 || !int.TryParse(fields[0], out int id) || id <= 0) continue;
                records[id] = fields;
            }
        }

        var localAnimals = FindObjectsByType<AnimalController>(FindObjectsSortMode.None);
        foreach (var animal in localAnimals)
        {
            if (animal != null && animal.NetworkAnimalId > 0 && !records.ContainsKey(animal.NetworkAnimalId))
                Destroy(animal.gameObject);
        }

        foreach (var pair in records)
        {
            string[] fields = pair.Value;
            if (!int.TryParse(fields[2], out int x) || !int.TryParse(fields[3], out int y) ||
                !int.TryParse(fields[4], out int z) || !int.TryParse(fields[5], out int hits)) continue;
            AnimalController animal = null;
            foreach (var existing in localAnimals)
                if (existing != null && existing.NetworkAnimalId == pair.Key) { animal = existing; break; }

            if (animal == null)
            {
                foreach (var spawner in All)
                {
                    if (spawner == null) continue;
                    GameObject prefab = spawner.ResolvePrefab(fields[1]);
                    if (prefab == null) continue;
                    nextNetworkAnimalId = Mathf.Max(nextNetworkAnimalId, pair.Key + 1);
                    spawner.SpawnLocal(prefab, new Vector3Int(x, y, z), pair.Key);
                    foreach (var candidate in FindObjectsByType<AnimalController>(FindObjectsSortMode.None))
                    {
                        if (candidate == null || candidate.NetworkAnimalId != pair.Key) continue;
                        animal = candidate;
                        break;
                    }
                    break;
                }
            }

            animal?.ApplyAuthoritativeState(new Vector3Int(x, y, z), hits);
        }
    }

    private GameObject ResolvePrefab(string prefabName)
    {
        foreach (var entry in schedule)
            if (entry.animalPrefab != null && entry.animalPrefab.name == prefabName) return entry.animalPrefab;
        foreach (var group in randomGroups)
        {
            if (group.animalPrefabs == null) continue;
            foreach (var candidate in group.animalPrefabs)
                if (candidate != null && candidate.name == prefabName) return candidate;
        }
        return null;
    }

    // ──────────────────────────────────────
    // スポーン保護ゾーン
    // ──────────────────────────────────────

    /// <summary>
    /// このスポナーに登録されている全スポーンセルを列挙する。
    /// defaultSpawnCell + schedule のカスタムセル + randomGroups の全セル。
    /// </summary>
    public System.Collections.Generic.IEnumerable<Vector3Int> GetAllSpawnCells()
    {
        yield return defaultSpawnCell;
        foreach (var e in schedule)
            if (!e.useDefaultSpawnCell)
                yield return e.spawnCell;
        foreach (var g in randomGroups)
            if (g.spawnCells != null)
                foreach (var c in g.spawnCells)
                    yield return c;
    }

    /// <summary>
    /// 指定セルがいずれかのスポナーの保護ゾーンに入っているかどうか。
    /// 各スポナーノの spawnProtectionRadius を尊重する。
    /// </summary>
    public static bool IsForbidden(Vector3Int cell)
    {
        foreach (var spawner in All)
        {
            if (spawner.spawnProtectionRadius <= 0) continue;
            foreach (var sc in spawner.GetAllSpawnCells())
                if (ChebyshevDistance(sc, cell) <= spawner.spawnProtectionRadius)
                    return true;
        }
        return false;
    }

    private static int ChebyshevDistance(Vector3Int a, Vector3Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
