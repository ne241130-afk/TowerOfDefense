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

    [Header("対象プレイヤー")]
    [Tooltip("0 = Host, 1 = Guest. ここを明示すると、画面側のズレを防げます。")]
    public int assignedPlayerIndex = -1;

    [Header("お金の増減エフェクト")]
    public MoneyEffectUI moneyEffect;

    private int previousMoney;
    private int lastLoggedTargetIndex = -99;
    private int lastLoggedServerP0 = int.MinValue;
    private int lastLoggedServerP1 = int.MinValue;
    private bool hasLoggedInitialServerValues;

    private void Start()
    {
        if (economyManager == null)
        {
            economyManager = EconomyManager.Instance;
        }

        if (economyManager == null)
        {
            Debug.LogError("MoneyUI: EconomyManager が見つかりません。");
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

    private void Update()
    {
        if (!MultiplayerRuntime.IsMultiplayerSession)
        {
            return;
        }

        RefreshFromServerState();
    }

    private void OnMoneyChanged(int newAmount)
    {
        if (MultiplayerRuntime.IsMultiplayerSession)
        {
            RefreshFromServerState();
            previousMoney = newAmount;
            return;
        }

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

        RefreshFromServerState();
        previousMoney = economyManager != null ? economyManager.CurrentMoney : newAmount;
    }

    private void RefreshFromServerState()
    {
        // Solo play has no network coordinator. Read the local economy directly so
        // OnMoneyChanged can refresh the UI without depending on multiplayer state.
        if (!MultiplayerRuntime.IsMultiplayerSession)
        {
            if (economyManager != null)
            {
                UpdateDisplay(economyManager.CurrentMoney);
                previousMoney = economyManager.CurrentMoney;
            }
            return;
        }

        if (MultiplayerMatchCoordinator.Instance == null)
        {
            return;
        }

        int targetIndex = ResolveTargetPlayerIndexForThisPanel();
        int serverP0 = MultiplayerMatchCoordinator.Instance.Player0Money;
        int serverP1 = MultiplayerMatchCoordinator.Instance.Player1Money;
        int targetMoney = targetIndex == 0 ? serverP0 : serverP1;

        int localEffective = MultiplayerRuntime.GetEffectiveLocalPlayerIndex();
        bool shouldLog = !hasLoggedInitialServerValues ||
            targetIndex != lastLoggedTargetIndex ||
            serverP0 != lastLoggedServerP0 ||
            serverP1 != lastLoggedServerP1 ||
            targetMoney != previousMoney;

        if (shouldLog)
        {
            Debug.Log($"[TRACE][MoneyUI] panelIndex={targetIndex} localEffective={localEffective} serverP0={serverP0} serverP1={serverP1} rendering={targetMoney}");
            lastLoggedTargetIndex = targetIndex;
            lastLoggedServerP0 = serverP0;
            lastLoggedServerP1 = serverP1;
            hasLoggedInitialServerValues = true;
        }

        UpdateDisplay(targetMoney);
        previousMoney = targetMoney;
    }

    private int ResolveTargetPlayerIndexForThisPanel()
    {
        if (assignedPlayerIndex == 0 || assignedPlayerIndex == 1)
        {
            return assignedPlayerIndex;
        }

        if (MultiplayerRuntime.IsMultiplayerSession)
        {
            int localIndex = MultiplayerRuntime.GetEffectiveLocalPlayerIndex();
            if (localIndex == 0 || localIndex == 1)
            {
                // Default to the local player's own money when the scene has not explicitly assigned a panel target.
                // This avoids leaking the wrong player's value into the client UI due to side-based guessing.
                assignedPlayerIndex = localIndex;
                return localIndex;
            }

            Debug.LogWarning($"[TRACE][MoneyUI] {gameObject.name} has no valid local player index yet; defaulting to 0 to avoid stale mismatch.");
        }

        return 0;
    }

    private void UpdateDisplay(int amount)
    {
        moneyText.text = $"Money: {amount}";
    }
}
