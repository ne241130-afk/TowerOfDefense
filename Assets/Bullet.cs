using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 20f;
    public float lifetime = 5f;
    
    private Vector2 direction;
    private float elapsedTime = 0f;
    private Rigidbody2D rb;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // Z 座標を -1 に設定
        Vector3 pos = transform.position;
        pos.z = -1f;
        transform.position = pos;
    }
    
    private void Update()
    {
        // 2D で移動
        rb.linearVelocity = direction * speed;
        
        // 一定時間後に消去
        elapsedTime += Time.deltaTime;
        if (elapsedTime >= lifetime)
        {
            Destroy(gameObject);
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