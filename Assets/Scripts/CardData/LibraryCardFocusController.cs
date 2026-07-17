using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Owns the "focus a library card" interaction shared by every card in the grid:
// the right-click fly-to-centre, the background blur behind it, the click-outside
// to return, and all the audio / visual tuning that a LibraryCardInteraction reads.
//
// One instance lives on the library window; LibraryManager hands it to each card
// it spawns. Only ever one card is lifted at a time — <see cref="_active"/> is that
// card (whether it's flying out or flying back), and <see cref="_focused"/> is the
// one currently settled at the centre and waiting for a click to dismiss it.
//
// A library card is a world-space transform rig: a plain Transform root carrying the
// CardVisualizer, with all graphics under a nested Canvas child. So the card is moved
// as a plain Transform and lifted above the blur by flipping that existing Canvas to
// overrideSorting. While focused it is reparented onto the root canvas so the Scroll
// View's viewport mask can't clip it, then reparented back into its slot on return.
//
// You do NOT have to place anything: if Background Blur is left empty the controller
// builds a full-screen backdrop under the root canvas at runtime. Assign your own only
// if you want to control its look/placement.
[DisallowMultipleComponent]
public class LibraryCardFocusController : MonoBehaviour
{
    [Header("Background blur")]
    [Tooltip("Optional. Full-screen panel with a CanvasGroup that fades in behind the " +
             "focused card. Leave EMPTY to have one built automatically under the root " +
             "canvas (recommended). Assign your own only to control its look.")]
    [SerializeField] CanvasGroup backgroundBlur;
    [Tooltip("Optional real screen blur (UIScreenBlur on the backdrop). Auto-added when " +
             "the backdrop is auto-created. Leave empty for a plain dim instead.")]
    [SerializeField] UIScreenBlur blurEffect;
    [Tooltip("Dim colour used when there's no UIScreenBlur (plain-dim fallback).")]
    [SerializeField] Color dimColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] float blurFadeDuration = 0.25f;

    [Header("Focus placement")]
    [Tooltip("Where the card flies to. Leave empty to use the centre of the screen.")]
    [SerializeField] RectTransform focusAnchor;
    [Tooltip("Card size while focused, as a multiple of its resting scale in the grid.")]
    [SerializeField] float focusScale = 2f;
    [Tooltip("Sorting order the focused card is lifted to. The blur is placed just below it.")]
    [SerializeField] int focusedSortingOrder = 1000;

    [Header("Fly animation")]
    [SerializeField] float flyToDuration = 0.45f;
    [SerializeField] float flyBackDuration = 0.38f;
    [Tooltip("Overshoot on the way out gives the card momentum. OutBack settles it lively.")]
    [SerializeField] Ease flyToEase = Ease.OutBack;
    [SerializeField] Ease flyBackEase = Ease.InOutCubic;
    [Tooltip("Quick rotational wiggle (degrees) as the card lands. 0 = no wiggle.")]
    [SerializeField] float landWiggleAngle = 7f;
    [SerializeField] float landWiggleDuration = 0.5f;
    [Tooltip("Vibrato of the landing wiggle: how many times it swings back and forth.")]
    [SerializeField] int landWiggleVibrato = 6;

    [Header("Hover glow")]
    [Tooltip("Opacity the glow ramps up to while a card is hovered. Its colour comes " +
             "from the card's resonance, so only the strength is set here.")]
    [Range(0f, 1f)]
    [SerializeField] float glowAlpha = 1f;
    [Tooltip("Seconds for the glow to ramp up when the pointer enters a card.")]
    [SerializeField] float glowFadeInDuration = 0.25f;
    [Tooltip("Seconds for the glow to ramp back down on pointer exit. A touch slower " +
             "than the fade-in reads as an afterglow.")]
    [SerializeField] float glowFadeOutDuration = 0.35f;
    [SerializeField] Ease glowFadeEase = Ease.OutSine;

    [Header("Audio (source your own clips)")]
    [Tooltip("Short blip played when the pointer enters a card.")]
    [SerializeField] AudioClip hoverClip;
    [Tooltip("Whoosh as the card flies to the centre.")]
    [SerializeField] AudioClip whooshToCentreClip;
    [Tooltip("Whoosh as the card flies back to its slot.")]
    [SerializeField] AudioClip whooshBackClip;

    // Read by LibraryCardInteraction to animate its card's resonance glow.
    public float GlowAlpha => glowAlpha;
    public float GlowFadeInDuration => glowFadeInDuration;
    public float GlowFadeOutDuration => glowFadeOutDuration;
    public Ease GlowFadeEase => glowFadeEase;

    public bool HasFocus => _focused != null;

    LibraryCardInteraction _focused; // settled at the centre, awaiting dismissal
    LibraryCardInteraction _active;  // currently lifted (flying out OR back)

    Canvas _rootCanvas;

    // Resting slot of _active, captured on lift so the return lands exactly home.
    Transform _homeParent;
    int _homeSiblingIndex;
    Vector3 _homeWorldPos;
    Vector3 _homeLocalScale;
    Quaternion _homeLocalRot;
    Vector3 _flyBaseScale; // the card's local scale after being reparented to the root canvas

    // The card's own Canvas, flipped to overrideSorting while focused; restored after.
    Canvas _liftCanvas;
    bool _prevOverrideSorting;
    int _prevSortingOrder;

    Tween _moveTween;
    Tween _scaleTween;
    Tween _rotTween;
    Tween _blurTween;

    readonly List<RaycastResult> _raycastHits = new();

    void Awake()
    {
        Canvas c = GetComponentInParent<Canvas>();
        _rootCanvas = c != null ? c.rootCanvas : null;

        if (backgroundBlur == null) backgroundBlur = BuildBackdrop();
        SetupBackdrop();
    }

    // While a card is focused, a click anywhere that isn't on the card returns to the
    // library. Polling (rather than only the backdrop's own click) keeps this working
    // no matter where/whether a backdrop panel is placed.
    void Update()
    {
        if (_focused == null) return;
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            if (!PointerOverFocusedCard()) Unfocus();
        }
    }

    bool PointerOverFocusedCard()
    {
        if (_focused == null || EventSystem.current == null) return false;

        var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        _raycastHits.Clear();
        EventSystem.current.RaycastAll(ped, _raycastHits);

        Transform card = _focused.Card;
        for (int i = 0; i < _raycastHits.Count; i++)
        {
            GameObject go = _raycastHits[i].gameObject;
            if (go != null && go.transform.IsChildOf(card)) return true;
        }
        return false;
    }

    public void PlayHover()
    {
        if (hoverClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(hoverClip);
    }

    // Right-clicking a card lands here. Flies it out to the centre and blurs the rest.
    public void Focus(LibraryCardInteraction card)
    {
        if (card == null || _focused == card) return;

        // A different card is still lifted (focused, or mid-return) — snap it home first.
        if (_active != null && _active != card) FinalizeActive();

        _active = card;
        _focused = card;

        Transform t = card.Card;

        // Remember the slot so the return lands exactly where it started.
        _homeParent = t.parent;
        _homeSiblingIndex = t.GetSiblingIndex();
        _homeWorldPos = t.position;
        _homeLocalScale = t.localScale;
        _homeLocalRot = t.localRotation;

        card.HideGlow();
        AddLift(card);

        // Lift the card onto the root canvas so the Scroll View's mask can't clip it.
        RectTransform parentRect = _homeParent as RectTransform;
        if (_rootCanvas != null)
        {
            t.SetParent(_rootCanvas.transform, true);
            parentRect = (RectTransform)_rootCanvas.transform;
        }
        _flyBaseScale = t.localScale;

        KillCardTweens();
        Vector3 targetLocal = FocusLocalPoint(t, parentRect);
        _moveTween = t.DOLocalMove(targetLocal, flyToDuration).SetEase(flyToEase).SetUpdate(true);
        _scaleTween = t.DOScale(_flyBaseScale * focusScale, flyToDuration).SetEase(flyToEase).SetUpdate(true);
        if (landWiggleAngle > 0f)
        {
            _rotTween = t.DOPunchRotation(new Vector3(0f, 0f, landWiggleAngle),
                    landWiggleDuration, landWiggleVibrato, 0.7f)
                .SetDelay(flyToDuration * 0.5f)
                .SetUpdate(true);
        }

        ShowBlur(true);
        if (whooshToCentreClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(whooshToCentreClip);
    }

    // Clicking anywhere but the focused card returns to the library.
    public void Unfocus()
    {
        if (_focused == null) return;

        LibraryCardInteraction card = _focused;
        _focused = null; // stop further click-outs while it flies home

        ShowBlur(false);
        if (whooshBackClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(whooshBackClip);

        Transform t = card.Card;
        KillCardTweens();
        _moveTween = t.DOMove(_homeWorldPos, flyBackDuration).SetEase(flyBackEase).SetUpdate(true);
        _scaleTween = t.DOScale(_flyBaseScale, flyBackDuration).SetEase(flyBackEase).SetUpdate(true);
        _rotTween = t.DOLocalRotateQuaternion(_homeLocalRot, flyBackDuration).SetEase(flyBackEase).SetUpdate(true)
            .OnComplete(() => { if (_active == card) FinalizeActive(); });
    }

    // Snap the lifted card exactly back into its slot and drop the sorting lift.
    void FinalizeActive()
    {
        if (_active == null) return;

        KillCardTweens();
        Transform t = _active.Card;
        if (_homeParent != null)
        {
            t.SetParent(_homeParent, false);
            t.SetSiblingIndex(_homeSiblingIndex);
        }
        t.localPosition = Vector3.zero;
        t.localScale = _homeLocalScale;
        t.localRotation = _homeLocalRot;
        RemoveLift();
        _active = null;
    }

    // Lift the card above the blur by flipping its own Canvas to overrideSorting. That
    // Canvas already carries a GraphicRaycaster, so the focused card stays clickable and
    // clicking it does NOT count as a click-outside.
    void AddLift(LibraryCardInteraction card)
    {
        _liftCanvas = card.CardCanvas;
        if (_liftCanvas == null) return;
        _prevOverrideSorting = _liftCanvas.overrideSorting;
        _prevSortingOrder = _liftCanvas.sortingOrder;
        _liftCanvas.overrideSorting = true;
        _liftCanvas.sortingOrder = focusedSortingOrder;
    }

    void RemoveLift()
    {
        if (_liftCanvas == null) return;
        _liftCanvas.overrideSorting = _prevOverrideSorting;
        _liftCanvas.sortingOrder = _prevSortingOrder;
        _liftCanvas = null;
    }

    void ShowBlur(bool on)
    {
        if (backgroundBlur == null) return;
        backgroundBlur.blocksRaycasts = on;

        if (on && blurEffect != null)
        {
            // Snapshot the current library view first, then fade in once it's ready so
            // the capture happens while the backdrop is still invisible.
            if (blurEffect.Capture(() => FadeBlur(1f))) return;
        }

        FadeBlur(on ? 1f : 0f);
    }

    void FadeBlur(float toAlpha)
    {
        if (backgroundBlur == null) return;
        _blurTween?.Kill();
        _blurTween = DOTween.To(() => backgroundBlur.alpha, a => backgroundBlur.alpha = a, toAlpha, blurFadeDuration)
            .SetUpdate(true);
    }

    // Screen-centre (or focusAnchor) expressed in the card's current parent-local space,
    // so the move lands the card at the middle of the screen.
    Vector3 FocusLocalPoint(Transform card, RectTransform parentRect)
    {
        if (parentRect == null) return card.localPosition;

        Camera cam = (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _rootCanvas.worldCamera : null;

        Vector2 targetScreen = focusAnchor != null
            ? RectTransformUtility.WorldToScreenPoint(cam, focusAnchor.position)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, targetScreen, cam, out Vector2 local))
            return new Vector3(local.x, local.y, card.localPosition.z);

        return card.localPosition;
    }

    // Build a full-screen backdrop under the root canvas so nothing has to be placed by
    // hand: a CanvasGroup + RawImage + UIScreenBlur, stretched to fill.
    CanvasGroup BuildBackdrop()
    {
        if (_rootCanvas == null) return null;

        var go = new GameObject("LibraryFocusBackdrop",
            typeof(RectTransform), typeof(CanvasGroup), typeof(RawImage), typeof(UIScreenBlur));
        go.layer = _rootCanvas.gameObject.layer;

        var rt = (RectTransform)go.transform;
        rt.SetParent(_rootCanvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        blurEffect = go.GetComponent<UIScreenBlur>();
        return go.GetComponent<CanvasGroup>();
    }

    // Wire whatever backdrop we have (built or assigned) for sorting, raycasting and clicks.
    void SetupBackdrop()
    {
        if (backgroundBlur == null) return;

        var blurCanvas = backgroundBlur.GetComponent<Canvas>();
        if (blurCanvas == null) blurCanvas = backgroundBlur.gameObject.AddComponent<Canvas>();
        blurCanvas.overrideSorting = true;
        blurCanvas.sortingOrder = Mathf.Max(1, focusedSortingOrder - 50);

        if (backgroundBlur.GetComponent<GraphicRaycaster>() == null)
            backgroundBlur.gameObject.AddComponent<GraphicRaycaster>();

        var graphic = backgroundBlur.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
            // No real blur available -> use the panel itself as a plain dim.
            if (blurEffect == null || !blurEffect.Ready) graphic.color = dimColor;
        }

        var backdrop = backgroundBlur.GetComponent<LibraryFocusBackdrop>();
        if (backdrop == null) backdrop = backgroundBlur.gameObject.AddComponent<LibraryFocusBackdrop>();
        backdrop.Init(this);

        backgroundBlur.alpha = 0f;
        backgroundBlur.blocksRaycasts = false;
    }

    void KillCardTweens()
    {
        _moveTween?.Kill();
        _scaleTween?.Kill();
        _rotTween?.Kill();
    }
}

// Sits on the background-blur panel and turns a click anywhere on it into a return
// to the library. Added automatically by LibraryCardFocusController. (Redundant with
// the controller's own click polling, but harmless — Unfocus is idempotent.)
[DisallowMultipleComponent]
public class LibraryFocusBackdrop : MonoBehaviour, IPointerClickHandler
{
    LibraryCardFocusController _controller;

    public void Init(LibraryCardFocusController controller) => _controller = controller;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_controller != null) _controller.Unfocus();
    }
}
