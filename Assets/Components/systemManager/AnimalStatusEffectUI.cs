using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AnimalStatusEffectUI : MonoBehaviour
{
    [Header("表示するImage")]
    public Image effectImage;

    [Header("汗エフェクト")]
    public Sprite[] sweatFrames;

    [Header("ハートエフェクト")]
    public Sprite[] heartFrames;

    [Header("アニメーション設定")]
    public float frameDuration = 0.1f;

    private Coroutine currentCoroutine;

    private void Start()
    {
        if (effectImage != null)
        {
            effectImage.enabled = false;
        }
    }

    public void ShowSweat()
    {
        PlayEffect(sweatFrames);
    }

    public void ShowHeart()
    {
        PlayEffect(heartFrames);
    }

    private void PlayEffect(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        if (effectImage == null)
        {
            Debug.LogWarning("AnimalStatusEffectUI: effectImage が設定されていません。");
            return;
        }

        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }

        currentCoroutine = StartCoroutine(PlayFrames(frames));
    }

    private IEnumerator PlayFrames(Sprite[] frames)
    {
        effectImage.enabled = true;

        foreach (Sprite frame in frames)
        {
            effectImage.sprite = frame;

            yield return new WaitForSeconds(frameDuration);
        }

        effectImage.enabled = false;
        currentCoroutine = null;
    }
}