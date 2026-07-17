using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Reveals a history tile's target stack with a quick, springy pop when the pointer
/// hovers the tile's main actor ("relevant unit"). At rest the target container is
/// tucked away — shrunk toward the unit and faded out; on hover it snaps up to its
/// authored pose with an <see cref="Ease.OutBack"/> overshoot so it reads as flung
/// out with a bit of inertia, then settles.
///
/// Drop this on the relevant-unit GameObject (the actor portrait — it must have a
/// raycast-target Graphic so it receives pointer events) and point <see cref="container"/>
/// at the DamageTargetContainer. The authored anchoredPosition / localScale of the
/// container become the shown pose, so you lay the container out normally in the
/// prefab and this only drives it between that pose and a tucked-in resting pose.
///
/// A CanvasGroup is added to the container if absent, so the hidden state also stops
/// the tucked target views from catching raycasts.
/// </summary>
[DisallowMultipleComponent]
public class HistoryTargetsPopout : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("What pops")]
    [Tooltip("The target stack to reveal (the tile's DamageTargetContainer). Its authored " +
             "anchoredPosition and localScale are captured as the shown pose.")]
    [SerializeField] RectTransform container;

    [Header("Resting (tucked-away) pose")]
    [Tooltip("Scale of the container while hidden, as a fraction of its shown scale. " +
             "0 = collapsed to nothing before it pops.")]
    [SerializeField, Range(0f, 1f)] float hiddenScale = 0f;
    [Tooltip("Offset from the shown position while hidden, in the container's local units. " +
             "Negative Y tucks it down toward the unit so it springs upward on hover.")]
    [SerializeField] Vector2 hiddenOffset = new Vector2(0f, -60f);
    [Tooltip("Fade the container out while hidden. Also drops blocksRaycasts so the tucked " +
             "target views don't intercept clicks meant for the tile.")]
    [SerializeField] bool fadeWhileHidden = true;

    [Header("Pop")]
    [Tooltip("Seconds for the pop-out. Kept short so it feels snappy.")]
    [SerializeField] float popDuration = 0.28f;
    [Tooltip("Overshoot-and-settle ease — the 'inertia' of the pop. OutBack flings past the " +
             "target and springs back; try OutElastic for a bouncier feel.")]
    [SerializeField] Ease popEase = Ease.OutBack;
    [Tooltip("Extra overshoot for OutBack (1.70158 is the DOTween default). Higher = more fling.")]
    [SerializeField] float popOvershoot = 2.2f;

    [Header("Retract")]
    [Tooltip("Seconds for the container to tuck back when the pointer leaves. Snappier than the pop.")]
    [SerializeField] float retractDuration = 0.14f;
    [SerializeField] Ease retractEase = Ease.InBack;

    [Tooltip("Start tucked away and only reveal on hover. Untick to have it shown by default.")]
    [SerializeField] bool startHidden = true;

    // Shown pose, captured from the container's authored transform in Awake.
    Vector2 _shownPos;
    Vector3 _shownScale;
    CanvasGroup _group;

    Tween _moveTween;
    Tween _scaleTween;
    Tween _fadeTween;

    void Awake()
    {
        if (container == null) return;

        _shownPos = container.anchoredPosition;
        _shownScale = container.localScale;

        if (fadeWhileHidden)
        {
            _group = container.GetComponent<CanvasGroup>();
            if (_group == null) _group = container.gameObject.AddComponent<CanvasGroup>();
        }
    }

    void OnEnable()
    {
        // Tiles are spawned fresh per entry; snap straight to the resting pose so the
        // first hover eases in cleanly (no leftover tween state).
        if (startHidden) SetHidden();
        else SetShown();
    }

    void OnDisable() => KillTweens();

    public void OnPointerEnter(PointerEventData eventData) => Show();
    public void OnPointerExit(PointerEventData eventData) => Hide();

    void Show()
    {
        if (container == null || !HasVisibleTargets()) return;
        KillTweens();

        _moveTween = container.DOAnchorPos(_shownPos, popDuration).SetEase(popEase, popOvershoot);
        _scaleTween = container.DOScale(_shownScale, popDuration).SetEase(popEase, popOvershoot);
        if (_group != null)
        {
            _group.blocksRaycasts = true;
            _fadeTween = _group.DOFade(1f, popDuration * 0.6f);
        }
    }

    void Hide()
    {
        if (container == null) return;
        KillTweens();

        _moveTween = container.DOAnchorPos(_shownPos + hiddenOffset, retractDuration).SetEase(retractEase);
        _scaleTween = container.DOScale(_shownScale * hiddenScale, retractDuration).SetEase(retractEase);
        if (_group != null)
        {
            _group.blocksRaycasts = false;
            _fadeTween = _group.DOFade(0f, retractDuration);
        }
    }

    // Nothing to pop when the entry has no targets (all pooled views are hidden).
    bool HasVisibleTargets()
    {
        for (int i = 0; i < container.childCount; i++)
            if (container.GetChild(i).gameObject.activeSelf) return true;
        return false;
    }

    void SetHidden()
    {
        KillTweens();
        container.anchoredPosition = _shownPos + hiddenOffset;
        container.localScale = _shownScale * hiddenScale;
        if (_group != null)
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }
    }

    void SetShown()
    {
        KillTweens();
        container.anchoredPosition = _shownPos;
        container.localScale = _shownScale;
        if (_group != null)
        {
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
        }
    }

    void KillTweens()
    {
        _moveTween?.Kill();
        _scaleTween?.Kill();
        _fadeTween?.Kill();
    }
}
