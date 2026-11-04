using UnityEngine;

public static class MultiplayerRuntime
{
    public static bool IsMultiplayerSession { get; private set; }
    public static bool IsGameplayReady { get; private set; }
    public static int LocalPlayerIndex { get; private set; } = -1;
    public static MultiplayerPlayerIdentity LocalPlayerIdentity { get; private set; }

    public static string StatusMessage { get; private set; } = string.Empty;

    public static void EnterMultiplayerSession()
    {
        IsMultiplayerSession = true;
        IsGameplayReady = false;
        StatusMessage = "接続中...";
    }

    public static void LeaveMultiplayerSession(string message)
    {
        IsMultiplayerSession = false;
        IsGameplayReady = false;
        LocalPlayerIndex = -1;
        LocalPlayerIdentity = null;
        StatusMessage = message;
    }

    public static void SetLocalPlayer(MultiplayerPlayerIdentity identity, int playerIndex)
    {
        LocalPlayerIdentity = identity;
        LocalPlayerIndex = playerIndex;
    }

    public static void MarkGameplayReady()
    {
        IsGameplayReady = true;
    }

    public static void SetStatus(string message)
    {
        StatusMessage = message;
    }

    public static bool IsPointerInsideOwnHalf()
    {
        if (!IsMultiplayerSession || LocalPlayerIndex < 0) return true;

        float normalized = Screen.width > 0 ? Input.mousePosition.x / Screen.width : 0.5f;
        if (LocalPlayerIndex == 0)
        {
            return normalized < 0.5f;
        }

        return normalized >= 0.5f;
    }
}
