using UnityEngine;

[CreateAssetMenu(fileName = "CommonBgmSettings", menuName = "Audio/Common BGM Settings")]
public class CommonBgmSettings : ScriptableObject
{
    [Header("Playback")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float volume = 1f;
    public bool loop = true;

    [Header("Play In Scenes")]
    public string[] playableSceneNames = { "StartScene", "Battle", "MultiBattleScene" };

    [Header("Stop In Scenes")]
    public string[] stopSceneNames = { "GameOverScene" };
}