using UnityEngine;

/// <summary>
/// 捕獲した動物の総数を管理するシングルトン。
/// 勝利条件(指定数の動物を捕まえる)の判定は、これを参照して実装する想定。
/// </summary>
public class CaptureCounter : MonoBehaviour
{
    public static CaptureCounter Instance { get; private set; }

    public int CapturedCount { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddCapture()
    {
        CapturedCount++;
        Debug.Log($"[CaptureCounter] 捕獲数: {CapturedCount}");
        // TODO: 勝利条件判定やUI(クリア目標欄)の更新はここから呼ぶ
    }
}
