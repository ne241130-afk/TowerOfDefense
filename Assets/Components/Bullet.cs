using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 20f;
    public float lifetime = 5f;
    [SerializeField] private bool homing = true;
    [SerializeField] private float homingInterval = 0.25f; // ターゲット再取得間隔（秒）

    private Vector2 direction;
    private float elapsedTime = 0f;
    private Rigidbody2D rb;

    // 内部タイマー
    private float homingTimer;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // Z 座標を -1 に設定
        Vector3 pos = transform.position;
        pos.z = -1f;
        transform.position = pos;

        homingTimer = 0f;
    }

    private void Update()
    {
        // ライフタイマー
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // 一定間隔でターゲット方向を更新
        if (homing)
        {
            homingTimer -= Time.fixedDeltaTime;
            if (homingTimer <= 0f)
            {
                UpdateHomingTarget();
                homingTimer = homingInterval;
            }
        }

        rb.linearVelocity = direction * speed;
    }

    private void UpdateHomingTarget()
    {
        Enemy_tag[] enemies = FindObjectsOfType<Enemy_tag>();
        if (enemies == null || enemies.Length == 0)
        {
            return;
        }

        Transform closest = null;
        float closestDistance = float.MaxValue;
        Vector3 myPos = transform.position;

        foreach (Enemy_tag e in enemies)
        {
            if (e == null) continue;
            float d = Vector2.SqrMagnitude(e.transform.position - myPos);
            if (d < closestDistance)
            {
                closestDistance = d;
                closest = e.transform;
            }
        }

        if (closest != null)
        {
            Vector2 newDir = (closest.position - transform.position).normalized;
            direction = newDir;
        }
    }

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
    }
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            Destroy(collision.gameObject);
            Destroy(gameObject);
        }
    }
}