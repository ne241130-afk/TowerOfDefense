using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WorrierMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.4f;
    [SerializeField] private float targetRefreshInterval = 0.25f;

    private Rigidbody2D body2d;
    private Transform target;
    private float refreshTimer;

    private void Awake()
    {
        body2d = GetComponent<Rigidbody2D>();
        body2d.bodyType = RigidbodyType2D.Kinematic;
        body2d.gravityScale = 0f;
    }

    private void FixedUpdate()
    {
        refreshTimer -= Time.fixedDeltaTime;
        if (refreshTimer <= 0f || target == null)
        {
            target = FindClosestEnemy();
            refreshTimer = targetRefreshInterval;
        }

        if (target == null)
        {
            return;
        }

        Vector2 nextPosition = Vector2.MoveTowards(
            body2d.position,
            target.position,
            moveSpeed * Time.fixedDeltaTime
        );

        body2d.MovePosition(nextPosition);
    }

    private Transform FindClosestEnemy()
    {
        Enemy_tag[] enemies = FindObjectsOfType<Enemy_tag>();
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach (Enemy_tag enemy in enemies)
        {
            if (enemy == null)
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = enemy.transform;
            }
        }

        return closest;
    }
}
