using UnityEngine;

[RequireComponent(typeof(Damageable))]
[RequireComponent(typeof(CombatCooldown))]
public class WorrierCombat : MonoBehaviour
{
    [SerializeField] private int attackPower = 1;
    [SerializeField] private int receivedDamage = 1;
    [SerializeField] private float damageCooldownSeconds = 1f;

    private Damageable selfHealth;
    private CombatCooldown selfCooldown;

    private void Awake()
    {
        selfHealth = GetComponent<Damageable>();
        selfCooldown = GetComponent<CombatCooldown>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryAttack(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryAttack(other);
    }

    private void TryAttack(Collider2D other)
    {
        if (!other.CompareTag("Enemy") && other.GetComponent<Enemy_tag>() == null)
        {
            return;
        }

        CombatCooldown enemyCooldown = other.GetComponent<CombatCooldown>();
        if (!selfCooldown.CanDealOrReceiveDamage ||
            (enemyCooldown != null && !enemyCooldown.CanDealOrReceiveDamage))
        {
            return;
        }

        Damageable enemyHealth = other.GetComponent<Damageable>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(attackPower);
        }
        else
        {
            Destroy(other.gameObject);
        }

        selfHealth.TakeDamage(receivedDamage);
        selfCooldown.BlockDamageFor(damageCooldownSeconds);

        if (enemyCooldown != null)
        {
            enemyCooldown.BlockDamageFor(damageCooldownSeconds);
        }
    }
}
