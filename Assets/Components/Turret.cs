using UnityEngine;

public class Turret : MonoBehaviour
{
    [Header("参照")]
    public Transform firePoint;
    public GameObject bulletPrefab;
    
    [Header("パラメータ")]
    public float fireRate = 1.0f;
    public float shootingRange = 5f; // 射撃範囲を追加
    
    private float fireTimer = 0f;
    
    private void Start()
    {
        if (firePoint == null)
        {
            firePoint = transform;
        }
    }
    
    private void Update()
    {
        Enemy_tag targetEnemy = FindClosestEnemyInRange();
        
        if (targetEnemy != null)
        {
            if (fireTimer <= 0f)
            {
                FireBullet(targetEnemy);
                fireTimer = fireRate;
            }
        }
        
        fireTimer -= Time.deltaTime;
    }
    
    private Enemy_tag FindClosestEnemyInRange()
    {
        Enemy_tag[] allEnemies = FindObjectsOfType<Enemy_tag>();
        
        if (allEnemies.Length == 0)
            return null;
        
        Enemy_tag closest = null;
        float closestDistance = float.MaxValue;
        
        foreach (Enemy_tag enemy in allEnemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            
            // 射撃範囲内かつ最も近い敵を探す
            if (distance <= shootingRange && distance < closestDistance)
            {
                closestDistance = distance;
                closest = enemy;
            }
        }
        
        return closest;
    }
    
    private void FireBullet(Enemy_tag target)
    {
        if (bulletPrefab == null) return;
        
        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        
        if (bullet != null)
        {
            Vector2 fireDirection = (target.transform.position - firePoint.position).normalized;
            bullet.SetDirection(fireDirection);
        }
    }
}