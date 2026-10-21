
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
    private bool isLooping;

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

    // 一度だけ汗を再生
    public void ShowSweat()
    {
        PlayEffect(sweatFrames);
    }

    // 一度だけハートを再生
    public void ShowHeart()
    {
        PlayEffect(heartFrames);
    }

    // 汗を繰り返す／停止する
    public void SetSweatActive(bool active)
    {
        SetLoopActive(sweatFrames, active);
    }

    // ハートを繰り返す／停止する
    public void SetHeartActive(bool active)
    {
        SetLoopActive(heartFrames, active);
    }

    // 一度だけ再生する
    private void PlayEffect(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0 || effectImage == null)
        {
            Debug.LogWarning(
                "AnimalStatusEffectUI: 画像または effectImage が未設定です。",
                this
            );
            return;
        }

        StopCurrentEffect();

        isLooping = false;
        currentCoroutine = StartCoroutine(PlayFrames(frames));
    }

    private IEnumerator PlayFrames(Sprite[] frames)
    {
        effectImage.enabled = true;

        foreach (Sprite frame in frames)
        {
            if (frame == null)
                continue;

            effectImage.sprite = frame;
            yield return new WaitForSeconds(
                Mathf.Max(0.01f, frameDuration)
            );
        }

        effectImage.enabled = false;
        currentCoroutine = null;
    }

    // 条件が続く間、繰り返し再生する
    private void SetLoopActive(Sprite[] frames, bool active)
    {
        if (active &&
            (frames == null || frames.Length == 0 || effectImage == null))
        {
            Debug.LogWarning(
                "AnimalStatusEffectUI: ループ用画像または effectImage が未設定です。",
                this
            );
            return;
        }

        // すでに同じループが動いている場合は再スタートしない
        if (active && isLooping && currentCoroutine != null)
            return;

        StopCurrentEffect();

        if (!active)
        {
            if (effectImage != null)
                effectImage.enabled = false;

            return;
        }

        isLooping = true;
        currentCoroutine = StartCoroutine(LoopEffect(frames));
    }

    private IEnumerator LoopEffect(Sprite[] frames)
    {
        effectImage.enabled = true;

        while (true)
        {
            foreach (Sprite frame in frames)
            {
                if (frame == null)
                    continue;

                effectImage.sprite = frame;
                yield return new WaitForSeconds(
                    Mathf.Max(0.01f, frameDuration)
                );
            }
        }
    }

    private void StopCurrentEffect()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }

        isLooping = false;
    }

    private void OnDisable()
    {
        StopCurrentEffect();

        if (effectImage != null)
            effectImage.enabled = false;
    }
}
