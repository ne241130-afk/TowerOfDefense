using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WaveManager : MonoBehaviour
{
    [SerializeField] private EnemySpawnController enemySpawnController;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private TextMeshProUGUI waveInfoUI;
    
    // Wave設定
    [SerializeField] private float waveDuration = 10f; // 各Wave継続時間
    
    // 出現間隔（秒）
    [SerializeField] private float wave1SpawnInterval = 3f;
    [SerializeField] private float wave2SpawnInterval = 2f;
    [SerializeField] private float wave3SpawnInterval = 1f; // Max

    // 移動速度の基本値と増加量
    [SerializeField] private float wave1MoveSpeed = 1.5f;
    [SerializeField] private float moveSpeedIncreasePerWave = 0.2f; // Wave4以降の速度増加量

    // Wave管理変数
    private int currentWave = 0;
    private float waveTimer = 0f;
    private bool waveActive = false;

    private void Start()
    {
        if (enemySpawnController == null)
        {
            enemySpawnController = FindObjectOfType<EnemySpawnController>();
        }

        // waveInfoUIがInspectorで設定されていなければ作成
        if (waveInfoUI == null && uiCanvas != null)
        {
            CreateWaveUI();
        }

        StartWave();
    }

    private void Update()
    {
        if (!waveActive)
        {
            return;
        }

        waveTimer += Time.deltaTime;

        // UI更新
        UpdateUI();

        // Wave継続時間を超えたら次のWaveへ
        if (waveTimer >= waveDuration)
        {
            NextWave();
        }
    }

    private void StartWave()
    {
        currentWave++;
        waveTimer = 0f;
        waveActive = true;

        float spawnInterval = GetSpawnInterval();
        float moveSpeed = GetMoveSpeed();

        enemySpawnController.SetWaveParameters(spawnInterval, moveSpeed);

        Debug.Log($"Wave {currentWave} Start! Spawn Interval: {spawnInterval}s, Move Speed: {moveSpeed}");
    }

    private void NextWave()
    {
        StartWave();
    }

    private float GetSpawnInterval()
    {
        return currentWave switch
        {
            1 => wave1SpawnInterval,
            2 => wave2SpawnInterval,
            _ => wave3SpawnInterval // Wave3以降は1秒固定
        };
    }

    private float GetMoveSpeed()
    {
        return currentWave switch
        {
            1 => wave1MoveSpeed,
            2 => wave1MoveSpeed + moveSpeedIncreasePerWave,
            3 => wave1MoveSpeed + moveSpeedIncreasePerWave * 2,
            _ => wave1MoveSpeed + moveSpeedIncreasePerWave * (currentWave - 1) // Wave4以降も増加し続ける
        };
    }

    private void UpdateUI()
    {
        if (waveInfoUI == null)
        {
            return;
        }

        float remainingTime = Mathf.Max(0, waveDuration - waveTimer);
        float spawnInterval = GetSpawnInterval();
        float moveSpeed = GetMoveSpeed();

        waveInfoUI.text = $"Wave: {currentWave}\n" +
                          $"Time: {remainingTime:F1}s\n" +
                          $"Spawn: {spawnInterval:F1}s\n" +
                          $"Speed: {moveSpeed:F2}";
    }

    private void CreateWaveUI()
    {
        GameObject uiObject = new GameObject("WaveInfo");
        uiObject.transform.SetParent(uiCanvas.transform, false);

        RectTransform rectTransform = uiObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);  // 左上
        rectTransform.anchorMax = new Vector2(0, 1);  // 左上
        rectTransform.pivot = new Vector2(0, 1);      // 左上
        rectTransform.anchoredPosition = new Vector2(20, -20);
        rectTransform.sizeDelta = new Vector2(280, 120);

        waveInfoUI = uiObject.AddComponent<TextMeshProUGUI>();
        waveInfoUI.text = "Wave: 0\nTime: 0s\nSpawn: 0s\nSpeed: 0";
        waveInfoUI.alignment = TextAlignmentOptions.TopLeft;
        waveInfoUI.fontSize = 36;
        waveInfoUI.color = Color.white;
        
        // テキストに黒いアウトラインを追加
        Outline outline = uiObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
    }

    public int GetCurrentWave()
    {
        return currentWave;
    }

    public float GetWaveProgress()
    {
        return waveTimer / waveDuration;
    }
}
