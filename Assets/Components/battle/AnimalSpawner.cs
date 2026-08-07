using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ターン制の動物スポナー。
/// 一定ターンごとに、指定したスポーン地点(セル)へ動物を1体生成する。
///
/// スポーン地点に既に動物がいる場合はそのマスが空くまで生成を待つため、
/// 複数体が同じ場所に重なって出現することはない
/// (turnsPerSpawnに達した後は、空くまで毎ターン再チャレンジし続ける)。
/// </summary>
public class AnimalSpawner : MonoBehaviour, ITurnActor
{
    [Header("生成設定")]
    [Tooltip("動物を生成する基準セル座標")]
    public Vector3Int spawnCell = new Vector3Int(0, -16, 0);

    [Tooltip("何ターンごとに1体生成するか")]
    public int turnsPerSpawn = 5;

    [Tooltip("生成する動物プレハブの候補。この中からランダムに1体選ばれる")]
    public List<GameObject> animalPrefabs = new List<GameObject>();

    private int turnCounter = 0;

    private void Start()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
        }
        else
        {
            Debug.LogError("AnimalSpawner: TurnManager が見つかりません。");
        }
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null) TurnManager.Instance.Unregister(this);
    }

    public void OnTurnTick()
    {
        turnCounter++;
        if (turnCounter < turnsPerSpawn) return;

        // 規定ターン数に達した。スポーン地点が空いていれば生成してカウンタをリセット、
        // 塞がっていれば空くまで毎ターン再チャレンジする(その間は生成しない = 重複出現を防止)
        if (TrySpawn())
        {
            turnCounter = 0;
        }
    }

    private bool TrySpawn()
    {
        if (animalPrefabs == null || animalPrefabs.Count == 0)
        {
            Debug.LogWarning("AnimalSpawner: animalPrefabsが空です。");
            return false;
        }

        if (FieldGridConfig.Instance == null || FieldGridConfig.Instance.grid == null)
        {
            Debug.LogWarning("AnimalSpawner: FieldGridConfigが見つかりません。");
            return false;
        }

        if (AnimalOccupancyMap.Instance != null && AnimalOccupancyMap.Instance.IsOccupied(spawnCell))
        {
            // スポーン地点に先客がいる間は生成しない
            return false;
        }

        GameObject prefab = animalPrefabs[Random.Range(0, animalPrefabs.Count)];
        Vector3 worldPos = FieldGridConfig.Instance.grid.GetCellCenterWorld(spawnCell);
        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);

        // AnimalController.Start()が呼ばれるより前に、この場で即座に占有を登録しておく
        // (同じターン内で他の動物の経路探索がこのマスを参照しても正しくブロックされるようにするため)
        var controller = obj.GetComponent<AnimalController>();
        if (controller != null && AnimalOccupancyMap.Instance != null)
        {
            AnimalOccupancyMap.Instance.SetOccupied(spawnCell, controller);
        }

        return true;
    }
}
