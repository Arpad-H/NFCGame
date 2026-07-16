using DG.Tweening;
using UnityEngine;

/// <summary>
/// Flips a button into view around its X axis whenever it — or any panel above it —
/// gets activated. It waits edge-on at 90°, which on this canvas is a zero-height
/// sliver and so invisible, then falls into the flip, swings through flat, overshoots,
/// and rocks a couple of times before settling.
///
/// The menu canvas is Screen Space - Overlay and renders orthographically, so there is
/// no perspective to tumble through: an X rotation reads as a vertical squash, which is
/// exactly what a flip looks like from the side. <see cref="ResonanceCoinReveal"/>
/// spins its coins on the same axis for the same reason.
///
/// The motion is a damped harmonic released from rest rather than a stock ease, because
/// the two ends of the ask pull in different directions: DOTween's OutBack overshoots
/// once and stops, and OutElastic rings but leaves the start at full speed (a snap, not
/// a swing). Released from rest it starts still, accelerates under its own spring, and
/// rings down — a door swinging shut. See <see cref="Damped"/>.
///
/// This only ever writes localRotation, so it is safe to stack on a button that already
/// has <see cref="MenuButtonMotion"/> — that script owns localScale and anchoredPosition
/// and never touches rotation.
///
/// OnEnable fires when a parent is activated too, so a lone button needs nothing but
/// this component. To flip a whole panel's buttons in a set order, add a
/// <see cref="MenuFlipGroup"/> to the panel and list them: a button the group owns holds
/// its edge-on pose until the group calls it, instead of flipping immediately.
///
/// Runs on unscaled time, like the rest of the menu juice.
/// </summary>
[DisallowMultipleComponent]
public class MenuFlipIn : MonoBehaviour
{
    [Header("Flip")]
    [Tooltip("Rotation the button waits at before flipping in. 90 is dead edge-on, so " +
             "it starts invisible; less than that lets it peek before it swings.")]
    [SerializeField] private float startAngle = 90f;
    [Tooltip("Seconds for the whole move, wobble included. The flip itself lands in " +
             "roughly the first third — the rest is the button rocking to a stop.")]
    [SerializeField] private float duration = 0.55f;

    [Header("Wobble")]
    [Tooltip("Roughly how many times the button swings back past flat before it settles. " +
             "0 just eases into place with no overshoot.")]
    [SerializeField, Min(0)] private int wobbles = 2;
    [Tooltip("How fast the rocking dies out. Higher lands harder and settles sooner; " +
             "lower keeps swinging. ~2 is very loose, ~4 is a clear rock, ~6 is tight.")]
    [SerializeField, Min(0.5f)] private float damping = 4f;

    [Header("Timing")]
    [Tooltip("Seconds to wait before flipping. Ignored when a MenuFlipGroup owns this " +
             "button — the group's stagger sets the delay instead.")]
    [SerializeField, Min(0f)] private float delay = 0f;
    [Tooltip("Flip on activation. Turn off to drive it from code or a UnityEvent only.")]
    [SerializeField] private bool playOnEnable = true;

    private Quaternion _homeRot;
    private Tween _flip;

    // Resolved lazily rather than in Awake: when a panel is activated, Unity is free to
    // run this OnEnable before the group's, and a group on an inactive panel may not have
    // Awoken at all yet. GetComponentInParent needs neither to have happened.
    private MenuFlipGroup _owner;
    private bool _ownerResolved;

    private MenuFlipGroup Owner
    {
        get
        {
            if (_ownerResolved) return _owner;
            _ownerResolved = true;
            var group = GetComponentInParent<MenuFlipGroup>(true);
            _owner = (group != null && group.Owns(this)) ? group : null;
            return _owner;
        }
    }

    private void Awake()
    {
        _homeRot = transform.localRotation;
    }

    private void OnEnable()
    {
        if (!playOnEnable) return;

        // Owned by a group: hold the edge-on pose so the button can't show for a frame
        // if the group's OnEnable runs after this one. The group plays it with its stagger.
        if (Owner != null)
        {
            SetAngle(startAngle);
            return;
        }

        Play(delay);
    }

    private void OnDisable()
    {
        // Panels get toggled with SetActive; leave the button flat so a re-show that
        // never reaches Play (playOnEnable off, dropped from a group's list) still
        // shows a readable button rather than an invisible sliver.
        _flip?.Kill();
        _flip = null;
        SnapHome();
    }

    /// <summary>Flip the button in from its edge-on start, after an optional delay.</summary>
    public void Play(float startDelay = 0f)
    {
        _flip?.Kill();
        SetAngle(startAngle);

        if (duration <= 0f)
        {
            SnapHome();
            return;
        }

        float omega = Mathf.PI * (0.5f + wobbles);
        // The ring-down is asymptotic, so it is still a fraction of a degree off flat at
        // t = 1. Bleeding that leftover out on a ramp lands it dead flat instead of
        // snapping the last degree away, which is what you'd see at low damping.
        float residual = Damped(1f, omega);

        _flip = DOVirtual.Float(0f, 1f, duration,
                t => SetAngle(startAngle * (Damped(t, omega) - t * residual)))
            .SetEase(Ease.Linear)   // the spring maths below is the easing
            .SetDelay(startDelay)
            .SetUpdate(true)
            .OnComplete(SnapHome);
    }

    /// <summary>Drop the button flat where it was authored, killing any flip in flight.</summary>
    public void SnapHome()
    {
        _flip?.Kill();
        _flip = null;
        transform.localRotation = _homeRot;
    }

    // Damped harmonic released from rest, normalised to 1 -> 0:
    //     u(t) = e^(-λt) · (cos(ωt) + (λ/ω)·sin(ωt))
    // u(0) = 1 and u'(0) = 0, so the button starts held at startAngle and dead still,
    // then accelerates into the swing under its own spring — that stillness at the top
    // is what reads as inertia. u'(t) = -e^(-λt)·(λ²/ω + ω)·sin(ωt) only changes sign
    // when sin(ωt) does, so it never creeps *past* startAngle on the way out; it swings
    // down through flat, overshoots to the far side, and each swing back is smaller than
    // the last by e^(-λ·period). ω sets how many swings fit in the duration, λ how
    // quickly they shrink.
    private float Damped(float t, float omega)
    {
        return Mathf.Exp(-damping * t)
             * (Mathf.Cos(omega * t) + (damping / omega) * Mathf.Sin(omega * t));
    }

    private void SetAngle(float angle)
    {
        // Composed onto the authored rotation, so a button that was laid out at a tilt
        // flips back to that tilt rather than to zero.
        transform.localRotation = _homeRot * Quaternion.Euler(angle, 0f, 0f);
    }
}
