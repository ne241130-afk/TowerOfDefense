using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MoneyEffectUI : MonoBehaviour
{
    [Header("表示するImage")]
    public Image effectImage;

    [Header("アニメーション設定")]
    public float frameDuration = 0.02f;

    private Sprite[] upFrames;
    private Sprite[] downFrames;

    // エフェクトを順番待ちさせるキュー
    private Queue<Sprite[]> effectQueue = new Queue<Sprite[]>();

    private Coroutine currentCoroutine;

    private void Awake()
    {
        // 画像を読み込む
        upFrames = Resources.LoadAll<Sprite>("MoneyEffect/MoneyUp");
        downFrames = Resources.LoadAll<Sprite>("MoneyEffect/MoneyDown");

        if (upFrames.Length == 0)
        {
            Debug.LogWarning("MoneyEffectUI: 上昇画像が見つかりません。");
        }

        if (downFrames.Length == 0)
        {
            Debug.LogWarning("MoneyEffectUI: 下降画像が見つかりません。");
        }
    }

    private void Start()
    {
        if (effectImage != null)
        {
            effectImage.enabled = false;
        }
    }

    public void ShowUp()
    {
        Debug.Log("★★★★★ ShowUp が呼ばれた ★★★★★");
        Debug.Log("upFrames枚数: " + upFrames.Length);

        foreach (Sprite sprite in upFrames)
        {
            Debug.Log("UP画像: " + sprite.name);
        }

        AddEffect(upFrames);
    }

    public void ShowDown()
    {
        Debug.Log("★★★★★ ShowDown が呼ばれた ★★★★★");
        Debug.Log("downFrames枚数: " + downFrames.Length);

        foreach (Sprite sprite in downFrames)
        {
            Debug.Log("DOWN画像: " + sprite.name);
        }

        AddEffect(downFrames);
    }

    private void AddEffect(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        // エフェクトをキューに追加
        effectQueue.Enqueue(frames);

        // まだ再生中でなければ再生開始
        if (currentCoroutine == null)
        {
            currentCoroutine = StartCoroutine(PlayQueuedEffects());
        }
    }

    private IEnumerator PlayQueuedEffects()
    {
        while (effectQueue.Count > 0)
        {
            // 次のエフェクトを取り出す
            Sprite[] frames = effectQueue.Dequeue();

            // エフェクト再生
            effectImage.enabled = true;

            foreach (Sprite frame in frames)
            {
                effectImage.sprite = frame;

                yield return new WaitForSeconds(frameDuration);
            }

            // このエフェクトが終わったら非表示
            effectImage.enabled = false;

            // 少し待ってから次のエフェクト
            yield return null;
        }

        currentCoroutine = null;
    }
}