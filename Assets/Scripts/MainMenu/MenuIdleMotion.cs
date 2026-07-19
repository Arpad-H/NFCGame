using UnityEngine;

/// <summary>
/// Gives a static menu element a little life: a slow vertical float and/or a subtle
/// breathing scale. Meant for the logo and other decorative bits — "everything moves
/// by about 2%".
///
/// Do NOT stack this on a button that has <see cref="MenuButtonMotion"/>; that script
/// already owns its transform and does its own breathing. Works on both UI
/// (RectTransform, moves anchoredPosition) and plain world Transforms.
/// </summary>
[DisallowMultipleComponent]
public class MenuIdleMotion : MonoBehaviour
{
    const float Tau = 6.2831853f;

    [Header("Float (vertical bob)")]
    [SerializeField] bool enableFloat = true;
    [Tooltip("Peak offset from centre, in pixels (UI) or units (world). 4 = drifts ±4.")]
    [SerializeField] float floatAmplitude = 4f;
    [Tooltip("Seconds for one full up-down cycle.")]
    [SerializeField] float floatPeriod = 6f;

    [Header("Breathe (scale pulse)")]
    [SerializeField] bool enableBreathe = false;
    [Tooltip("Peak scale added: 0.02 = 1.00 -> 1.02 -> 1.00.")]
    [SerializeField] float breatheAmount = 0.02f;
    [SerializeField] float breathePeriod = 4f;

    [Tooltip("Offset the phase randomly so several elements don't move in unison.")]
    [SerializeField] bool randomizePhase = true;

    RectTransform _rect;
    Vector2 _baseAnchored;
    Vector3 _baseLocalPos;
    Vector3 _baseScale;
    float _floatPhase;
    float _breathePhase;

    void Awake()
    {
        _rect = transform as RectTransform;
        _baseScale = transform.localScale;
        if (_rect != null) _baseAnchored = _rect.anchoredPosition;
        else _baseLocalPos = transform.localPosition;

        if (randomizePhase)
        {
            _floatPhase = Random.Range(0f, floatPeriod);
            _breathePhase = Random.Range(0f, breathePeriod);
        }
    }

    // Re-capture the resting pose every time we're switched on, so a controller that
    // repositions or rescales this element while we're off (e.g. GameOverScreen popping
    // the panel in) has the float/breathe settle around the NEW pose rather than the
    // stale one cached at Awake. Harmless for always-on users: it re-reads the same pose.
    void OnEnable()
    {
        if (_rect == null) _rect = transform as RectTransform;
        _baseScale = transform.localScale;
        if (_rect != null) _baseAnchored = _rect.anchoredPosition;
        else _baseLocalPos = transform.localPosition;
    }

    void OnDisable()
    {
        // Snap back so a hidden element doesn't reappear mid-drift.
        if (_rect != null) _rect.anchoredPosition = _baseAnchored;
        else transform.localPosition = _baseLocalPos;
        transform.localScale = _baseScale;
    }

    void Update()
    {
        float t = Time.unscaledTime;

        if (enableFloat && floatPeriod > 0f)
        {
            float y = Mathf.Sin((t + _floatPhase) * Tau / floatPeriod) * floatAmplitude;
            if (_rect != null) _rect.anchoredPosition = _baseAnchored + Vector2.up * y;
            else transform.localPosition = _baseLocalPos + Vector3.up * y;
        }

        if (enableBreathe && breathePeriod > 0f)
        {
            float pulse = 1f + breatheAmount * 0.5f
                * (1f - Mathf.Cos((t + _breathePhase) * Tau / breathePeriod));
            transform.localScale = _baseScale * pulse;
        }
    }
}
