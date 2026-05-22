using UnityEngine;

public class GameOverScreen : MonoBehaviour
{
    private GUIStyle titleStyle;

    private void OnGUI()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "GAME OVER", titleStyle);
    }
}
