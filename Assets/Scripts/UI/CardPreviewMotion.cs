using UnityEngine;

/// <summary>
/// Gives the hover card-preview a bit of life: a slow vertical "floating" bob with a
/// faint roll, plus a mouse-reactive parallax tilt where the edge of the card nearest
/// the cursor leans away from the screen — as if you were pressing that corner back.
///
/// Lives on any centred-pivot RectTransform inside the CardPreview prefab that isn't
/// itself positioned by a parent layout: the card faces (CardMinion / CardSpell) and the
/// keyword-panel holder. Those have a centred pivot, so the tilt rotates around their
/// middle, and <see cref="CardPreviewUI"/> positions the parent Canvas — not these
/// objects — so the motion here never fights the preview's placement. Give the keyword
/// holder a Phase Offset so it bobs slightly out of sync with the card. Everything runs on
/// unscaled time so it keeps playing while the game is paused.
///
/// The tilt reads the global cursor rather than pointer events: the pointer actually sits
/// on the board card beside the preview, so the preview leans toward that card and keeps
/// reacting as you slide over it. Flip <see cref="leanAway"/> or the invert toggles if a
/// direction feels reversed for your canvas setup.
/// </summary>
[DisallowMultipleComponent]
public class CardPreviewMotion : MonoBehaviour
{
    const float Tau = 6.2831853f;

    [Header("Floating bob")]
    [Tooltip("Vertical drift in the card's local units (± peak). The card-face rect is 100 tall.")]
    [SerializeField] float bobAmplitude = 7f;
    [SerializeField] float bobPeriod = 3.2f;
    [Tooltip("Gentle roll (Z) swaying alongside the bob, degrees (± peak).")]
    [SerializeField] float swayAngle = 1.4f;
    [SerializeField] float swayPeriod = 4.6f;

    [Header("Mouse parallax tilt")]
    [Tooltip("Peak pitch/yaw as the cursor reaches the react radius, degrees.")]
    [SerializeField] float tiltAmount = 10f;
    [Tooltip("How far the cursor travels from the card centre to reach full tilt, in card " +
             "half-sizes. Bigger = the tilt builds up more gradually as the cursor roams.")]
    [SerializeField] float reactSpread = 2.5f;
    [Tooltip("Subtle counter-shift of the card against the cursor for extra depth, local units.")]
    [SerializeField] float positionParallax = 6f;
    [Tooltip("Edge nearest the cursor leans away from the screen. Untick to lean toward it.")]
    [SerializeField] bool leanAway = true;
    [Tooltip("Flip if the horizontal tilt feels backwards for your canvas.")]
    [SerializeField] bool invertX = false;
    [Tooltip("Flip if the vertical tilt feels backwards for your canvas.")]
    [SerializeField] bool invertY = false;

    [Header("Phase")]
    [Tooltip("Randomise the float/roll start so stacked previews never pulse in unison. " +
             "Turn OFF to dial a deterministic offset with Phase Offset instead.")]
    [SerializeField] bool randomizePhase = false;
    [Tooltip("Seconds added to this element's float/roll phase. Offset the keyword panels " +
             "from the card face here so they bob slightly out of sync.")]
    [SerializeField] float phaseOffset = 0f;

    [Header("Feel")]
    [Tooltip("Seconds for the tilt/parallax to catch up to the cursor. Higher = floatier.")]
    [SerializeField] float smoothTime = 0.11f;

    RectTransform _rect;
    Canvas _canvas;

    // Authored rest pose, captured once. The parallax/bob compose on top of this, and
    // OnDisable restores it so re-shows never accumulate drift.
    Vector3 _restPos;
    Quaternion _restRot;

    // Smoothed cursor-driven state. .x = horizontal (nx), .y = vertical (ny), each -1..1.
    Vector2 _tilt;
    Vector2 _tiltVel;
    Vector2 _shift;
    Vector2 _shiftVel;
    float _phase;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        _restPos = _rect.localPosition;
        _restRot = _rect.localRotation;
        // Random desync, or a deterministic offset (used to slip the keyword panels out of
        // phase with the card face).
        _phase = randomizePhase ? Random.Range(0f, bobPeriod) : phaseOffset;
    }

    void OnEnable()
    {
        // Shown fresh each hover; start settled at rest so the first frame eases in.
        _tilt = _tiltVel = _shift = _shiftVel = Vector2.zero;
    }

    void OnDisable()
    {
        if (_rect == null) return;
        _rect.localPosition = _restPos;
        _rect.localRotation = _restRot;
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;

        // Cursor offset from this card's centre, normalised to -1..1 over the react radius.
        Vector2 target = Vector2.zero;
        if (Input.mousePresent)
        {
            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera
                : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, Input.mousePosition, cam, out Vector2 local))
            {
                Rect r = _rect.rect;
                float spanX = Mathf.Max(1f, r.width * 0.5f * reactSpread);
                float spanY = Mathf.Max(1f, r.height * 0.5f * reactSpread);
                target.x = Mathf.Clamp((local.x - r.center.x) / spanX, -1f, 1f);
                target.y = Mathf.Clamp((local.y - r.center.y) / spanY, -1f, 1f);
            }
        }

        _tilt = Vector2.SmoothDamp(_tilt, target, ref _tiltVel, smoothTime, Mathf.Infinity, dt);
        _shift = Vector2.SmoothDamp(_shift, target, ref _shiftVel, smoothTime, Mathf.Infinity, dt);

        // Cursor-side edge recedes: cursor above centre pitches the top back, cursor right
        // yaws the right edge back. leanAway/invert flip these for other canvas orientations.
        float dir = leanAway ? 1f : -1f;
        float pitch = -_tilt.y * tiltAmount * dir * (invertY ? -1f : 1f);
        float yaw = _tilt.x * tiltAmount * dir * (invertX ? -1f : 1f);

        // Continuous floating bob + roll (never smoothed — it's the idle heartbeat).
        float t = Time.unscaledTime + _phase;
        float bob = Mathf.Sin(t * Tau / bobPeriod) * bobAmplitude;
        float roll = Mathf.Sin(t * Tau / swayPeriod) * swayAngle;

        Vector3 pos = _restPos;
        pos.x += -_shift.x * positionParallax;
        pos.y += bob - _shift.y * positionParallax;

        _rect.localPosition = pos;
        _rect.localRotation = _restRot * Quaternion.Euler(pitch, yaw, roll);
    }
}
