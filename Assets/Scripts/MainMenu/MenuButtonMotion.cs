using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Juices a menu button: a gentle idle breathing pulse, golden glow + slight lift on
/// hover, and a quick punch-in on press. Drop it on the same GameObject as the
/// <see cref="Button"/>. Everything runs on unscaled time so it keeps working while
/// the game is paused.
///
/// Hover/press are tweened as scalar fields (so they keep their eases, including the
/// spring-back bounce) and composed with the breathing pulse in one place in
/// <see cref="LateUpdate"/> — that single writer to the transform is what stops the
/// idle motion and the hover motion from fighting over localScale.
///
/// Because the menu canvas is Screen Space - Overlay, real post-process bloom can't
/// reach the UI. The "gold bloom" is faked by fading in an optional additive glow
/// graphic placed behind the button (assign it to <see cref="glow"/>).
/// </summary>
[DisallowMultipleComponent]
public class MenuButtonMotion : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    const float Tau = 6.2831853f;

    [Header("Scale (multipliers on the button's resting scale)")]
    [SerializeField] float hoverScale = 1.05f;
    [SerializeField] float pressScale = 0.95f;

    [Header("Lift")]
    [Tooltip("Pixels the button rises while hovered (reference-resolution units).")]
    [SerializeField] float hoverLift = 5f;

    [Header("Idle breathing")]
    [SerializeField] bool enableBreathing = true;
    [Tooltip("Peak breathing scale added while idle: 0.02 = 1.00 -> 1.02 -> 1.00.")]
    [SerializeField] float breatheAmount = 0.02f;
    [SerializeField] float breathePeriod = 4f;
    [Tooltip("Offset the breathing phase randomly so nearby buttons don't pulse in unison.")]
    [SerializeField] bool randomizePhase = true;

    [Header("Timing")]
    [SerializeField] float tweenDuration = 0.15f;
    [SerializeField] Ease ease = Ease.OutCubic;
    [Tooltip("Extra bounce when the button springs back up after a click.")]
    [SerializeField] Ease releaseEase = Ease.OutBack;
    [Tooltip("Seconds for breathing to fade back in after the pointer leaves.")]
    [SerializeField] float breatheFade = 0.25f;

    [Header("Gold Glow (optional, faked bloom)")]
    [Tooltip("A soft additive gold sprite behind the button. Left invisible until hover.")]
    [SerializeField] Graphic glow;
    [SerializeField, Range(0f, 1f)] float glowAlpha = 1f;

    RectTransform _rect;
    Selectable _selectable;

    Vector3 _baseScale;
    Vector2 _basePos;

    // Hover/press are tweened into these scalars; LateUpdate composes them onto the
    // transform together with the breathing pulse, so there's only one writer.
    float _stateScale = 1f;
    float _stateLift;
    float _breatheWeight = 1f;
    float _breatheVel;
    float _phase;

    Tween _scaleTween;
    Tween _liftTween;
    Tween _glowTween;

    bool _hovering;
    bool _pressed;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _selectable = GetComponent<Selectable>();
        _baseScale = _rect.localScale;
        _basePos = _rect.anchoredPosition;
        _phase = randomizePhase ? Random.Range(0f, breathePeriod) : 0f;

        if (glow != null)
            SetGlowAlpha(0f);
    }

    void OnEnable()
    {
        // Panels get toggled with SetActive; start from a clean resting state.
        _hovering = false;
        _pressed = false;
        _stateScale = 1f;
        _stateLift = 0f;
        _breatheWeight = 1f;
    }

    void OnDisable()
    {
        KillTweens();
        _rect.localScale = _baseScale;
        _rect.anchoredPosition = _basePos;
        if (glow != null) SetGlowAlpha(0f);
    }

    bool Interactable => _selectable == null || _selectable.interactable;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!Interactable) return;
        _hovering = true;
        ApplyState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
        _pressed = false;
        ApplyState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable) return;
        _pressed = true;
        ApplyState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        ApplyState(springBack: true);
    }

    // Recomputes the hover/press target from (hovering, pressed) and tweens the
    // scalar fields toward it. Driving everything from one place keeps the states
    // from fighting each other.
    void ApplyState(bool springBack = false)
    {
        float factor = _pressed ? pressScale : (_hovering ? hoverScale : 1f);
        float lift = (_hovering && !_pressed) ? hoverLift : 0f;
        float targetGlow = _hovering ? glowAlpha : 0f;

        _scaleTween?.Kill();
        _scaleTween = DOTween.To(() => _stateScale, v => _stateScale = v, factor, tweenDuration)
            .SetEase(springBack ? releaseEase : ease)
            .SetUpdate(true);

        _liftTween?.Kill();
        _liftTween = DOTween.To(() => _stateLift, v => _stateLift = v, lift, tweenDuration)
            .SetEase(ease)
            .SetUpdate(true);

        if (glow != null)
        {
            _glowTween?.Kill();
            _glowTween = glow.DOFade(targetGlow, tweenDuration).SetUpdate(true);
        }
    }

    void LateUpdate()
    {
        // Breathing only while fully idle; fade its weight so returning from hover
        // doesn't pop.
        bool idle = enableBreathing && !_hovering && !_pressed;
        _breatheWeight = Mathf.SmoothDamp(_breatheWeight, idle ? 1f : 0f,
            ref _breatheVel, breatheFade, Mathf.Infinity, Time.unscaledDeltaTime);

        // (1 - cos)/2 rises 0 -> 1 -> 0 starting at rest, so the pulse only grows the
        // button (1.00 -> 1.02), never shrinks it.
        float pulse = 1f + breatheAmount * 0.5f
            * (1f - Mathf.Cos((Time.unscaledTime + _phase) * Tau / breathePeriod))
            * _breatheWeight;

        _rect.localScale = _baseScale * (_stateScale * pulse);
        _rect.anchoredPosition = _basePos + Vector2.up * _stateLift;
    }

    void SetGlowAlpha(float a)
    {
        Color c = glow.color;
        c.a = a;
        glow.color = c;
    }

    void KillTweens()
    {
        _scaleTween?.Kill();
        _liftTween?.Kill();
        _glowTween?.Kill();
    }
}
