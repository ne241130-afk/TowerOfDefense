using UnityEngine;
using UnityEngine.UI;

public class EscapeCountUI : MonoBehaviour
{
    public Text escapeCountText;

    void Update()
    {
        if (WaveManager.Instance == null) return;

        escapeCountText.text =
            "wave20まで どうぶつ の だっそう を そししよう！\n" +
            "どうぶつ が 5ひき にげたら まけ！\n" +
            "にげた：" + WaveManager.Instance.EscapeCount + "/5";
    }
}