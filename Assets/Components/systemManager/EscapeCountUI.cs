using UnityEngine;
using UnityEngine.UI;

public class EscapeCountUI : MonoBehaviour
{
    public Text escapeCountText;

    void Update()
    {
        if (WaveManager.Instance == null) return;

        escapeCountText.text =
            "wave20まで\n" +
            "動物の脱走を\n" +
            "阻止しよう！\n\n" +
            "動物が5匹\n" +
            "脱走したら\n" +
            "負け！\n\n" +
            "脱走数：" + WaveManager.Instance.EscapeCount + "/5";
    }
}