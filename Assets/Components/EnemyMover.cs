using UnityEngine;

//Rigidbody2Dのつけ忘れ防止
[RequireComponent(typeof(Rigidbody2D))]

public class EnemyMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    private Transform target;
    private Rigidbody2D enemyRigidbody;

    private void Awake()
    {
        enemyRigidbody = GetComponent<Rigidbody2D>();
        enemyRigidbody.bodyType = RigidbodyType2D.Kinematic;
        enemyRigidbody.gravityScale = 0f;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector2 nextPosition = Vector2.MoveTowards(
            enemyRigidbody.position,
            target.position,
            moveSpeed * Time.fixedDeltaTime
        );

        enemyRigidbody.MovePosition(nextPosition);
    }
    
}
