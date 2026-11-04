using UnityEngine;
using UnityEngine.SceneManagement;

public static class MultiplayerSceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnSceneLoaded()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.name == "StartScene")
        {
            StartMenuController controller = Object.FindFirstObjectByType<StartMenuController>();
            if (controller != null && controller.GetComponent<MultiplayerStartUI>() == null)
            {
                controller.gameObject.AddComponent<MultiplayerStartUI>();
            }
            return;
        }

        if (scene.name == "MultiBattleScene")
        {
            if (Object.FindFirstObjectByType<MultiplayerStatusOverlay>() == null)
            {
                GameObject go = new GameObject("MultiplayerStatusOverlay");
                go.AddComponent<MultiplayerStatusOverlay>();
            }

            if (Object.FindFirstObjectByType<MultiplayerSharedStateOverlay>() == null)
            {
                GameObject go = new GameObject("MultiplayerSharedStateOverlay");
                go.AddComponent<MultiplayerSharedStateOverlay>();
            }

            MultiplayerRuntime.EnterMultiplayerSession();
        }
    }
}
