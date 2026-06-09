using UnityEngine;

public class CombatCooldown : MonoBehaviour
{
    private float nextDamageTime;

    public bool CanDealOrReceiveDamage => Time.time >= nextDamageTime;

    public void BlockDamageFor(float seconds)
    {
        nextDamageTime = Mathf.Max(nextDamageTime, Time.time + seconds);
    }
}
