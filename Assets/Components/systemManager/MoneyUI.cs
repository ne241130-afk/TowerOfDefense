using UnityEngine;
using TMPro;

/// <summary>
/// EconomyManager の currentMoney を UI に表示する簡易コンポーネント。
/// Canvas 上の Text を割り当てて使う。
/// </summary>
public class MoneyUI : MonoBehaviour
{
    public EconomyManager economyManager;
    public TextMeshProUGUI moneyText; // TextMeshProUGUI を割り当て

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
        UpdateDisplay(newAmount);
    }

    private void UpdateDisplay(int amount)
    {
        moneyText.text = $"Money: {amount}";
    }
}