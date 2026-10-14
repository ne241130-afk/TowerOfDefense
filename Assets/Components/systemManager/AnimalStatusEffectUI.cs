
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
        else
        {
            Debug.LogWarning(
                "AnimalStatusEffectUI: effectImage が設定されていません。",
                this
            );
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
            Debug.LogWarning("AnimalStatusEffectUI: エフェクト画像が設定されていません。", this);
            return;
        }

        if (effectImage == null)
        {
            Debug.LogWarning("AnimalStatusEffectUI: effectImage が設定されていません。", this);
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
            if (frame == null)
            {
                continue;
            }

            effectImage.sprite = frame;
            yield return new WaitForSeconds(frameDuration);
        }

        effectImage.enabled = false;
        currentCoroutine = null;
    }
}
