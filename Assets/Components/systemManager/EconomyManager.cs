using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 所持金管理。ターンごとの収入を処理するシングルトン。
/// - Start() で TurnManager に登録 (TurnManager が存在することが前提)
/// - OnTurnTick() で incomePerTurn を加算
/// - TrySpend(amount) で支出を試行（成功なら true）
/// - ResetEconomy で初期化
/// </summary>
public class EconomyManager : MonoBehaviour, ITurnActor
{
    public static EconomyManager Instance { get; private set; }

    [Header("初期設定")]
    public int startingMoney = 100;
    [Tooltip("1ターン経過ごとに増える金額")]
    public int incomePerTurn = 10;

    [Header("状態（読み取り専用）")]
    [SerializeField] private int currentMoney = 0;
    public int CurrentMoney => currentMoney;

    [Header("イベント")]
    public UnityEvent<int> OnMoneyChanged; // 引数: currentMoney

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 必要なら永続化する（現在の設計に合わせてください）
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        currentMoney = startingMoney;
        OnMoneyChanged?.Invoke(currentMoney);

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Register(this);
        }
        else
        {
            Debug.LogWarning("EconomyManager: TurnManager が見つかりません。Start 時に登録できませんでした。");
        }
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.Unregister(this);
        }
        if (Instance == this) Instance = null;
    }

    // ITurnActor: ターン経過で収入を加算
    public void OnTurnTick()
    {
        AddMoney(incomePerTurn);
    }

    // 所持金を増やす（外部からも使用可能）
    public void AddMoney(int amount)
    {
        if (amount == 0) return;
        currentMoney += amount;
        if (currentMoney < 0) currentMoney = 0;
        OnMoneyChanged?.Invoke(currentMoney);
    }

    // 支払いを試みる。成功すれば true（所持金から減る）
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (currentMoney >= amount)
        {
            currentMoney -= amount;
            OnMoneyChanged?.Invoke(currentMoney);
            return true;
        }
        return false;
    }

    // 強制的に所持金を設定（デバッグやリセット用）
    public void SetMoney(int amount)
    {
        currentMoney = Mathf.Max(0, amount);
        OnMoneyChanged?.Invoke(currentMoney);
    }

    // リスタート時などの初期化
    public void ResetEconomy(int startMoney = -1)
    {
        currentMoney = (startMoney >= 0) ? startMoney : startingMoney;
        OnMoneyChanged?.Invoke(currentMoney);
    }
}