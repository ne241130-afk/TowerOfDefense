using UnityEngine;

//EnemyがTowerに攻撃して消える処理
public class EnemyAttackOnTouch : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Enemyが " + other.name + " に当たった");

        TowerHealth tower = other.GetComponent<TowerHealth>();
        if (tower == null)
        {
            return;
        }

        tower.TakeDamage(1);
        Destroy(gameObject);
    }
}
