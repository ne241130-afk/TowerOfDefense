using UnityEngine;
using TMPro;

/// <summary>
/// EconomyManager の currentMoney を UI に表示する簡易コンポーネント。
/// Canvas 上の Text を割り当てて使う。
/// </summary>
public class MoneyUI : MonoBehaviour
{
    public EconomyManager economyManager;
    public TextMeshProUGUI moneyText;

    [Header("お金の増減エフェクト")]
    public MoneyEffectUI moneyEffect;

    private int previousMoney;

    private void Start()
    {
        if (economyManager == null)
        {
            Debug.LogError("MoneyUI: economyManager を割り当ててください。");
            enabled = false;
            return;
        }

        if (moneyText == null)
        {
            Debug.LogError("MoneyUI: moneyText を割り当ててください。");
            enabled = false;
            return;
        }

        economyManager.OnMoneyChanged.AddListener(OnMoneyChanged);

        previousMoney = economyManager.CurrentMoney;

        UpdateDisplay(economyManager.CurrentMoney);
    }

    private void OnDestroy()
    {
        if (economyManager != null)
        {
            economyManager.OnMoneyChanged.RemoveListener(OnMoneyChanged);
        }
    }

    private void OnMoneyChanged(int newAmount)
    {
        Debug.Log($"MoneyUI [{gameObject.name}] : {previousMoney} → {newAmount}");

        if (newAmount > previousMoney)
        {
            Debug.Log("★★★ MoneyUI → ShowUp ★★★");

            if (moneyEffect != null)
            {
                moneyEffect.ShowUp();
            }
        }
        else if (newAmount < previousMoney)
        {
            Debug.Log("★★★ MoneyUI → ShowDown ★★★");

            if (moneyEffect != null)
            {
                moneyEffect.ShowDown();
            }
        }

        UpdateDisplay(newAmount);

        previousMoney = newAmount;
    }

    private void UpdateDisplay(int amount)
    {
        moneyText.text = $"Money: {amount}";
    }
}