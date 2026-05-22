using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    public const int DefaultAttackPower = 1;

    [SerializeField] private int attackPower = DefaultAttackPower;

    public int AttackPower => Mathf.Max(DefaultAttackPower, attackPower);
}
