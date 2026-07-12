using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Juices a menu button: golden glow + slight lift on hover, a quick punch-in on
/// press. Drop it on the same GameObject as the <see cref="Button"/>. Everything
/// animates on unscaled time so it keeps working while the game is paused.
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
    [Header("Scale (multipliers on the button's resting scale)")]
    [SerializeField] float hoverScale = 1.05f;
    [SerializeField] float pressScale = 0.95f;

    [Header("Lift")]
    [Tooltip("Pixels the button rises while hovered (reference-resolution units).")]
    [SerializeField] float hoverLift = 5f;

    [Header("Timing")]
    [SerializeField] float tweenDuration = 0.15f;
    [SerializeField] Ease ease = Ease.OutCubic;
    [Tooltip("Extra bounce when the button springs back up after a click.")]
    [SerializeField] Ease releaseEase = Ease.OutBack;

    [Header("Gold Glow (optional, faked bloom)")]
    [Tooltip("A soft additive gold sprite behind the button. Left invisible until hover.")]
    [SerializeField] Graphic glow;
    [SerializeField, Range(0f, 1f)] float glowAlpha = 1f;

    RectTransform _rect;
    Selectable _selectable;

    Vector3 _baseScale;
    Vector2 _basePos;

    Tween _scaleTween;
    Tween _moveTween;
    Tween _glowTween;

    bool _hovering;
    bool _pressed;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _selectable = GetComponent<Selectable>();
        _baseScale = _rect.localScale;
        _basePos = _rect.anchoredPosition;

        if (glow != null)
            SetGlowAlpha(0f);
    }

    void OnDisable()
    {
        // Panels get toggled with SetActive; snap back so a hidden button doesn't
        // reappear mid-animation next time it's shown.
        KillTweens();
        _hovering = false;
        _pressed = false;
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

    // Recomputes the visual target from (hovering, pressed) and tweens toward it.
    // Driving everything from one place keeps the states from fighting each other.
    void ApplyState(bool springBack = false)
    {
        float factor = _pressed ? pressScale : (_hovering ? hoverScale : 1f);
        float lift = (_hovering && !_pressed) ? hoverLift : 0f;
        float targetGlow = _hovering ? glowAlpha : 0f;

        _scaleTween?.Kill();
        _scaleTween = _rect.DOScale(_baseScale * factor, tweenDuration)
            .SetEase(springBack ? releaseEase : ease)
            .SetUpdate(true);

        _moveTween?.Kill();
        _moveTween = _rect.DOAnchorPos(_basePos + Vector2.up * lift, tweenDuration)
            .SetEase(ease)
            .SetUpdate(true);

        if (glow != null)
        {
            _glowTween?.Kill();
            _glowTween = glow.DOFade(targetGlow, tweenDuration).SetUpdate(true);
        }
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
        _moveTween?.Kill();
        _glowTween?.Kill();
    }
}
