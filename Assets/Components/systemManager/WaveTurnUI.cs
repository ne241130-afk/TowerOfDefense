using UnityEngine;
using TMPro;

/// <summary>
/// WaveManager の状態を Text に表示する軽量コンポーネント。
/// Inspector で waveManager, waveText, turnText を割り当てるだけで動く。
/// </summary>
public class WaveTurnUI : MonoBehaviour
{
    public WaveManager waveManager;
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI turnText;

    private void Start()
    {
        if (waveManager == null)
        {
            Debug.LogError("WaveTurnUI: waveManager を割り当ててください。");
            enabled = false;
            return;
        }

        waveManager.OnWaveStarted.AddListener(OnWaveStarted);
        waveManager.OnTurnChanged.AddListener(OnTurnChanged);

        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (waveManager != null)
        {
            waveManager.OnWaveStarted.RemoveListener(OnWaveStarted);
            waveManager.OnTurnChanged.RemoveListener(OnTurnChanged);
        }
    }

    private void OnWaveStarted(int wave) => UpdateDisplay();
    private void OnTurnChanged(int turn) => UpdateDisplay();

    private void UpdateDisplay()
    {
        if (waveText != null) waveText.text = $"Wave: {waveManager.currentWave}";
        if (turnText != null) turnText.text = $"Turn: {Mathf.Max(1, waveManager.turnInWave)}/{waveManager.turnsPerWave}";
    }
}