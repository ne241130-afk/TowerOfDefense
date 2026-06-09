using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 6f;
    [SerializeField] private float bobAmount = 0.04f;
    [SerializeField] private float bobSpeed = 8f;

    private SpriteRenderer spriteRenderer;
    private Vector3 initialLocalPosition;
    private bool canBob;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialLocalPosition = transform.localPosition;
        canBob = GetComponent<Rigidbody2D>() == null;
    }

    private void Update()
    {
        if (frames != null && frames.Length > 0)
        {
            int frameIndex = Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length;
            spriteRenderer.sprite = frames[frameIndex];
        }

        if (canBob && bobAmount > 0f)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            transform.localPosition = initialLocalPosition + new Vector3(0f, bob, 0f);
        }
    }
}
