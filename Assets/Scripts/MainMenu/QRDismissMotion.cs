using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Dismisses a QR panel with some physicality once its player has joined: the panel
/// first pops toward the camera with a springy overshoot, hangs for a beat, then loses
/// its footing and freefalls off the bottom of the screen. Only once it is gone is the
/// GameObject disabled.
///
/// The menu canvas is Screen Space - Overlay, so there is no real perspective to push
/// into — "toward the camera" is faked with scale, the same trick the coin reveal uses
/// at the peak of its arc. The springiness is the OutBack overshoot on that scale: it
/// sails past the target and settles back, which is what reads as inertia.
///
/// The fall is driven in world space rather than anchoredPosition so it clears the
/// screen no matter how the panel is nested, anchored or scaled inside the canvas, and
/// it accelerates (InQuad) because a constant-speed drop reads as floating.
///
/// Everything runs on unscaled time so it keeps working while the game is paused,
/// matching the rest of the menu juice.
/// </summary>
[DisallowMultipleComponent]
public class QRDismissMotion : MonoBehaviour
{
    [Header("Pop toward the camera")]
    [Tooltip("Size at the top of the pop, as a multiplier on the panel's resting scale. " +
             "The canvas is Overlay, so scale is what 'coming toward the camera' looks like.")]
    [SerializeField] private float popScale = 1.15f;
    [SerializeField] private float popDuration = 0.28f;
    [Tooltip("Springiness of the pop: how far it sails past popScale before settling back. " +
             "0 = no overshoot, ~1.7 is DOTween's default, higher = looser and bouncier.")]
    [SerializeField] private float popOvershoot = 2.5f;
    [Tooltip("Seconds the panel hangs at its popped size before gravity takes it.")]
    [SerializeField] private float hangTime = 0.08f;

    [Header("Freefall")]
    [Tooltip("Seconds to clear the screen. Shorter = heavier.")]
    [SerializeField] private float fallDuration = 0.55f;
    [Tooltip("Extra drop past the bottom of the canvas, in multiples of the panel's own " +
             "height, so it is fully out of frame before the GameObject is disabled.")]
    [SerializeField] private float fallMargin = 1f;
    [Tooltip("Degrees the panel tumbles on its way down (direction is random per drop). " +
             "0 = a dead-straight fall.")]
    [SerializeField] private float fallTumble = 18f;

    private RectTransform _rect;
    private Vector2 _homePos;
    private Vector3 _homeScale;
    private Quaternion _homeRot;
    private Sequence _sequence;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _homePos = _rect.anchoredPosition;
        _homeScale = _rect.localScale;
        _homeRot = _rect.localRotation;
    }

    private void OnEnable()
    {
        // A fresh lobby re-shows this panel with SetActive, but the last drop left it
        // somewhere below the screen — put it back where it was authored.
        RestHome();
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;
    }

    /// <summary>
    /// Pop the panel toward the camera, then drop it off the bottom of the screen and
    /// disable this GameObject. Safe to call on an already-hidden panel.
    /// </summary>
    public void Play(Action onComplete = null)
    {
        _sequence?.Kill();
        _sequence = null;
        RestHome();

        // Nothing to animate if the panel isn't on screen; honour the caller's contract
        // (panel ends up hidden) without leaving a tween running on a dead object.
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
            return;
        }

        float targetY = _rect.position.y - FallWorldDistance();
        float tumble = fallTumble * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

        _sequence = DOTween.Sequence()
            .Append(_rect.DOScale(_homeScale * popScale, popDuration)
                .SetEase(Ease.OutBack, popOvershoot))
            .AppendInterval(hangTime)
            .Append(_rect.DOMoveY(targetY, fallDuration)
                .SetEase(Ease.InQuad))
            .Join(_rect.DOLocalRotate(new Vector3(0f, 0f, tumble), fallDuration,
                    RotateMode.LocalAxisAdd)
                .SetEase(Ease.InOutSine))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                gameObject.SetActive(false); // OnDisable clears _sequence
                onComplete?.Invoke();
            });
    }

    // A world-space drop that always clears the bottom of the canvas, whatever this
    // panel's nesting or anchoring inside it. Falls back to the screen height if the
    // panel somehow isn't under a canvas at all.
    private float FallWorldDistance()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null
            ? canvas.rootCanvas.transform as RectTransform
            : null;

        float canvasHeight = canvasRect != null
            ? canvasRect.rect.height * Mathf.Abs(canvasRect.lossyScale.y)
            : Screen.height;

        // Measured at the popped size, since that's how big it is while falling.
        float selfHeight = _rect.rect.height * Mathf.Abs(_rect.lossyScale.y) * popScale;

        return canvasHeight + selfHeight * (1f + Mathf.Max(0f, fallMargin));
    }

    private void RestHome()
    {
        if (_rect == null) return;
        _rect.anchoredPosition = _homePos;
        _rect.localScale = _homeScale;
        _rect.localRotation = _homeRot;
    }
}
