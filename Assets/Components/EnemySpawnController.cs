using UnityEngine;

public class EnemySpawnController : MonoBehaviour
{
    //生成する敵
    [SerializeField] private GameObject commonEnemyPrefab;
    //敵を出す位置
    [SerializeField] private Transform spawnPoint;
    //敵の目的地
    [SerializeField] private Transform target;
    //生成間隔
    [SerializeField] private float spawnInterval = 3f;
    //Towerが壊れてるか否か
    [SerializeField] private TowerHealth towerHealth;

    //タイマー
    private float timer;

    private void Update()
    {
        //TowrのHPが0なら何もしない
        if (towerHealth != null && towerHealth.IsDestroyed)
        {
            return;
        }

        //時間を進める
        timer += Time.deltaTime;

        //まだ時間になってないなら終了
        if (timer < spawnInterval)
        {
            return;
        }

        //タイマーリセット
        timer = 0f;

        //敵生成
        SpawnEnemy(commonEnemyPrefab);
    }

    private void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null || spawnPoint == null)
        {
            return;
        }
        
        //prefabをコピー
        GameObject enemy = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

        EnemyMover mover = enemy.GetComponent<EnemyMover>();
        if (mover != null)
        {
            mover.SetTarget(target);
        }
}
    
}
