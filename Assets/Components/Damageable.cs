using UnityEngine;

public class Damageable : MonoBehaviour
{
    [SerializeField] private int maxHp = 3;

    private int hp;

    public bool IsDead => hp <= 0;

    private void Awake()
    {
        hp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        hp = Mathf.Max(0, hp - damage);

        if (hp <= 0)
        {
            Destroy(gameObject);
        }
    }
}
