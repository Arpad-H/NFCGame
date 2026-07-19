using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Drives the game-over screen's entrance: the panel pops into place with a
// high-energy overshoot-and-fade while a real screen blur crossfades in behind
// it, from sharp (0) up to a predefined strength.
//
// The blur reuses the same UIScreenBlur "freeze-frame" component the card
// library uses: it grabs one screenshot, blurs that snapshot, and shows it on a
// full-screen RawImage. There is no live/animatable blur radius — the "gradual
// blur" is a CanvasGroup alpha crossfade from the sharp live screen (alpha 0) to
// the fully-blurred snapshot (alpha blurTargetAlpha). UIScreenBlur requires a
// RawImage, which is why the old plain-Image "blurrBackground" couldn't host it;
// instead this controller auto-builds a proper blur backdrop at runtime (exactly
// like LibraryCardFocusController.BuildBackdrop) so nothing has to be wired by hand.
//
// Put this on the GameOverScreen root. Call Show(winnerName) when the match ends
// (e.g. from GameManager.GameOver). The screen starts hidden in Awake, so leaving
// it active in the scene is fine.
[DisallowMultipleComponent]
public class GameOverScreen : MonoBehaviour
{
    [Header("Panel pop")]
    [Tooltip("The content that pops in (scale + fade). Defaults to this object's " +
             "transform, so the whole screen — text and artwork — pops as one.")]
    [SerializeField] RectTransform panel;
    [Tooltip("Faded 0 -> 1 as the panel pops. Auto-added to the panel if empty.")]
    [SerializeField] CanvasGroup panelGroup;
    [Tooltip("Winner name shown when Show() is given one. Optional.")]
    [SerializeField] TMP_Text winnerLabel;
    [Tooltip("Scale the panel starts at before popping to full size. 0.6 = grows from 60%.")]
    [SerializeField] float popStartScale = 0.6f;
    [SerializeField] float popDuration = 0.55f;
    [Tooltip("OutBack overshoot: how far past full size it springs before settling. " +
             "Higher = livelier. 1.7 is DOTween's default.")]
    [SerializeField] float popOvershoot = 2.2f;
    [Tooltip("Panel fade is faster than the scale so it's readable at the peak of the pop.")]
    [SerializeField] float fadeDuration = 0.3f;
    [Tooltip("Sorting order the panel is lifted to (above the blur, above the HUD).")]
    [SerializeField] int panelSortingOrder = 900;

    [Header("Background blur")]
    [Tooltip("Optional. A full-screen CanvasGroup+RawImage+UIScreenBlur backdrop. " +
             "Leave empty to have one built automatically (recommended).")]
    [SerializeField] CanvasGroup blurGroup;
    [Tooltip("Optional real screen blur on the backdrop. Auto-added when the backdrop " +
             "is auto-built. Empty -> a plain dim (dimColor) instead of a blur.")]
    [SerializeField] UIScreenBlur blurEffect;
    [Tooltip("How much the blurred snapshot dominates at the end. 1 = fully blurred; " +
             "lower leaves the sharp screen partly showing. This is the 'predefined blur'.")]
    [Range(0f, 1f)] [SerializeField] float blurTargetAlpha = 1f;
    [Tooltip("Seconds for the blur to fade in. Match/roughly match the pop for a cohesive feel.")]
    [SerializeField] float blurFadeDuration = 0.55f;
    [SerializeField] Ease blurFadeEase = Ease.OutQuad;
    [Tooltip("Fallback dim colour used only when there is no UIScreenBlur (plain-dim mode).")]
    [SerializeField] Color dimColor = new Color(0f, 0f, 0f, 0.72f);

    [Header("Placeholder cleanup")]
    [Tooltip("The old plain-white 'blurrBackground' Image. Auto-found by name if empty; " +
             "disabled on startup so only the real blur shows.")]
    [SerializeField] GameObject legacyPlaceholder;

    [Header("Audio")]
    [Tooltip("Optional sting played when the screen shows.")]
    [SerializeField] AudioClip showClip;

    Canvas _rootCanvas;
    Vector3 _panelBaseScale;
    Tween _blurTween;
    Tween _scaleTween;
    Tween _fadeTween;

    // Idle float/breathe on the panel. It writes localScale every frame (when
    // breathe is on), which fights the pop tween — so we switch it off during the
    // reveal and switch it back on once the panel has settled at full size.
    MenuIdleMotion _idleMotion;

    void Awake()
    {
        Canvas c = GetComponentInParent<Canvas>();
        _rootCanvas = c != null ? c.rootCanvas : null;

        if (panel == null) panel = transform as RectTransform;
        _panelBaseScale = panel != null ? panel.localScale : Vector3.one;
        _idleMotion = panel != null ? panel.GetComponent<MenuIdleMotion>() : null;

        if (panelGroup == null && panel != null)
        {
            panelGroup = panel.GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        // Lift the panel onto its own sorting layer so it always draws above the
        // blur backdrop (and the HUD), regardless of sibling order.
        if (panel != null)
        {
            var panelCanvas = panel.GetComponent<Canvas>();
            if (panelCanvas == null) panelCanvas = panel.gameObject.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = panelSortingOrder;
            if (panel.GetComponent<GraphicRaycaster>() == null)
                panel.gameObject.AddComponent<GraphicRaycaster>();
        }

        // Hide the old plain-white placeholder so only the real blur shows.
        if (legacyPlaceholder == null)
        {
            Transform t = transform.Find("blurrBackground");
            if (t != null) legacyPlaceholder = t.gameObject;
        }
        if (legacyPlaceholder != null) legacyPlaceholder.SetActive(false);

        if (blurGroup == null) blurGroup = BuildBlurBackdrop();
        SetupBlur();

        // Start fully hidden — Show() reveals it.
        SetHiddenImmediate();
    }

    // Reveal the game-over screen: capture and crossfade the blur in behind the
    // panel while the panel pops. Pass the winning player's display name to fill
    // the label; pass null/empty to leave the label as authored.
    public void Show(string winnerName = null)
    {
        gameObject.SetActive(true);

        if (!string.IsNullOrEmpty(winnerName) && winnerLabel != null)
            winnerLabel.text = winnerName;

        if (showClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(showClip);

        SetHiddenImmediate();

        if (blurGroup != null) blurGroup.blocksRaycasts = true;

        // Snapshot the current (still panel-invisible) screen first, then crossfade
        // the blur and pop the panel once it's ready, so the capture never includes
        // the panel or feeds the backdrop back on itself. Falls back to an immediate
        // plain dim + pop if the blur can't run.
        if (blurEffect != null && blurEffect.Capture(RevealNow)) return;
        RevealNow();
    }

    // Fade the blur and panel back out. Leaves the object active (alpha 0); call
    // gameObject.SetActive(false) after if you want it gone entirely.
    public void Hide()
    {
        KillTweens();
        SetIdleMotion(false); // own the scale so the shrink isn't fought
        if (blurGroup != null) blurGroup.blocksRaycasts = false;

        if (blurGroup != null)
            _blurTween = FadeGroup(blurGroup, 0f, blurFadeDuration * 0.6f, Ease.InQuad);
        if (panelGroup != null)
            _fadeTween = FadeGroup(panelGroup, 0f, fadeDuration, Ease.InQuad);
        if (panel != null)
            _scaleTween = panel.DOScale(_panelBaseScale * popStartScale, fadeDuration)
                .SetEase(Ease.InBack).SetUpdate(true);
    }

    void RevealNow()
    {
        KillTweens();
        // The pop owns the panel's scale — mute the idle breathe/float so it can't
        // fight the tween (which shows up as a snap on the settle from overshoot).
        SetIdleMotion(false);

        if (blurGroup != null)
            _blurTween = FadeGroup(blurGroup, blurTargetAlpha, blurFadeDuration, blurFadeEase);

        if (panelGroup != null)
            _fadeTween = FadeGroup(panelGroup, 1f, fadeDuration, Ease.OutQuad);

        if (panel != null)
        {
            panel.localScale = _panelBaseScale * popStartScale;
            _scaleTween = panel.DOScale(_panelBaseScale, popDuration)
                .SetEase(Ease.OutBack, popOvershoot)
                .SetUpdate(true) // survive a timeScale = 0 pause on game over
                .OnComplete(ResumeIdleMotion);
        }
        else
        {
            ResumeIdleMotion();
        }
    }

    // Land the panel exactly at full scale, then hand the transform back to the
    // idle breathe/float so it settles around full size (not the popped-from scale).
    void ResumeIdleMotion()
    {
        if (panel != null) panel.localScale = _panelBaseScale;
        SetIdleMotion(true);
    }

    void SetIdleMotion(bool on)
    {
        if (_idleMotion != null) _idleMotion.enabled = on;
    }

    void SetHiddenImmediate()
    {
        KillTweens();
        SetIdleMotion(false);
        if (blurGroup != null) blurGroup.alpha = 0f;
        if (panelGroup != null) panelGroup.alpha = 0f;
        if (panel != null) panel.localScale = _panelBaseScale * popStartScale;
    }

    // DOTween's UI module isn't relied on here (matching the card library) — tween
    // the alpha field directly so this compiles regardless of module setup.
    Tween FadeGroup(CanvasGroup g, float to, float duration, Ease ease)
    {
        return DOTween.To(() => g.alpha, a => g.alpha = a, to, duration)
            .SetEase(ease).SetUpdate(true);
    }

    void KillTweens()
    {
        _blurTween?.Kill();
        _scaleTween?.Kill();
        _fadeTween?.Kill();
    }

    // Build a full-screen backdrop (CanvasGroup + RawImage + UIScreenBlur) under the
    // root canvas, on its own Canvas one step below the panel so it blurs the whole
    // screen but stays behind the panel. Mirrors LibraryCardFocusController.
    CanvasGroup BuildBlurBackdrop()
    {
        Transform parent = _rootCanvas != null ? _rootCanvas.transform : transform.parent;
        if (parent == null) return null;

        var go = new GameObject("GameOverBlurBackdrop",
            typeof(RectTransform), typeof(CanvasGroup), typeof(RawImage),
            typeof(UIScreenBlur), typeof(Canvas), typeof(GraphicRaycaster));
        go.layer = gameObject.layer;

        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var canvas = go.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = panelSortingOrder - 1;

        blurEffect = go.GetComponent<UIScreenBlur>();
        return go.GetComponent<CanvasGroup>();
    }

    // Wire whatever backdrop we have (built or assigned) and set its resting state.
    void SetupBlur()
    {
        if (blurGroup == null) return;

        var graphic = blurGroup.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
            // No real blur -> use the panel itself as a plain dim wash.
            if (blurEffect == null || !blurEffect.Ready) graphic.color = dimColor;
        }

        blurGroup.alpha = 0f;
        blurGroup.blocksRaycasts = false;
    }
}
