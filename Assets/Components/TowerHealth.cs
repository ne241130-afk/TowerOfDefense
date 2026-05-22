using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TowerHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 3;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string gameOverSceneName = "GameOverScene";

    private int currentHp;
    private bool isGameOver;

    public int CurrentHp => currentHp;

    private void Awake()
    {
        currentHp = maxHp;
    }

    private void Update()
    {
        if (IsDebugDamagePressed())
        {
            TakeDamage(1);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsEnemy(other.gameObject))
        {
            TakeDamage(GetEnemyAttackPower(other.gameObject));
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsEnemy(collision.gameObject))
        {
            TakeDamage(GetEnemyAttackPower(collision.gameObject));
        }
    }

    public void TakeDamage(int damage)
    {
        if (isGameOver || damage <= 0)
        {
            return;
        }

        currentHp = Mathf.Max(0, currentHp - damage);

        if (currentHp <= 0)
        {
            isGameOver = true;
            SceneManager.LoadScene(gameOverSceneName);
        }
    }

    private bool IsEnemy(GameObject target)
    {
        return target.CompareTag(enemyTag) || target.name == enemyTag;
    }

    private int GetEnemyAttackPower(GameObject enemy)
    {
        if (enemy.TryGetComponent(out EnemyAttack enemyAttack))
        {
            return enemyAttack.AttackPower;
        }

        return EnemyAttack.DefaultAttackPower;
    }

    private bool IsDebugDamagePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.K))
        {
            return true;
        }
#endif

        return false;
    }
}
