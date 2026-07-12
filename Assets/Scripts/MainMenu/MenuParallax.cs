using UnityEngine;

/// <summary>
/// Fakes depth on the main menu by scaling and translating the whole background as
/// the mouse moves, instead of shifting separate parallax layers. It reads like
/// you're looking around inside the world.
///
/// Put it on the full-screen background RectTransform (or assign one to
/// <see cref="target"/>). The background wants to be a touch larger than the screen
/// so the shift never exposes an edge; <see cref="baseScale"/> supplies that overscan
/// and <see cref="clampToScreen"/> guarantees an edge can never peek regardless of
/// the numbers you dial in.
/// </summary>
[DisallowMultipleComponent]
public class MenuParallax : MonoBehaviour
{
    [Header("Target (defaults to this RectTransform)")]
    [SerializeField] RectTransform target;

    [Header("Translation")]
    [Tooltip("Max shift from centre, in reference-resolution pixels (±).")]
    [SerializeField] float positionShift = 30f;
    [Tooltip("Background moves opposite the cursor, so you appear to look 'into' the scene.")]
    [SerializeField] bool invert = true;

    [Header("Scale")]
    [Tooltip("Resting overscan. Keep >1 so the shift never reveals an edge. " +
             "1.06 gives enough slack at 1080p to cover a ±30px shift.")]
    [SerializeField] float baseScale = 1.06f;
    [Tooltip("Extra scale added as the cursor reaches the corners (breathing).")]
    [SerializeField] float scaleAdd = 0.03f;

    [Header("Feel")]
    [Tooltip("Seconds to catch up to the cursor. Higher = floatier.")]
    [SerializeField] float smoothTime = 0.12f;
    [Tooltip("Never let the shift push the background off its own edges.")]
    [SerializeField] bool clampToScreen = true;

    Vector3 _baseScaleVec;
    Vector2 _basePos;

    Vector2 _pos;
    Vector2 _posVel;
    float _scale;
    float _scaleVel;

    void Awake()
    {
        if (target == null) target = (RectTransform)transform;
        _baseScaleVec = target.localScale;
        _basePos = target.anchoredPosition;
        _scale = baseScale;
        _pos = Vector2.zero;
    }

    void LateUpdate()
    {
        // Cursor as -1..1 from screen centre. No mouse (touch/unfocused) -> recentre.
        Vector2 n = Vector2.zero;
        if (Input.mousePresent && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 mp = Input.mousePosition;
            n.x = Mathf.Clamp(mp.x / Screen.width * 2f - 1f, -1f, 1f);
            n.y = Mathf.Clamp(mp.y / Screen.height * 2f - 1f, -1f, 1f);
        }

        float mag = Mathf.Clamp01(n.magnitude);
        float targetScale = baseScale + mag * scaleAdd;
        Vector2 targetPos = (invert ? -n : n) * positionShift;

        if (clampToScreen)
            targetPos = ClampToOverscan(targetPos, targetScale);

        float dt = Time.unscaledDeltaTime;
        _scale = Mathf.SmoothDamp(_scale, targetScale, ref _scaleVel, smoothTime, Mathf.Infinity, dt);
        _pos = Vector2.SmoothDamp(_pos, targetPos, ref _posVel, smoothTime, Mathf.Infinity, dt);

        target.localScale = _baseScaleVec * _scale;
        target.anchoredPosition = _basePos + _pos;
    }

    // Keep the shift within the slack created by scaling the background up, so its
    // edges stay outside the frame. Slack per side = size * (scale - 1) / 2.
    Vector2 ClampToOverscan(Vector2 offset, float scale)
    {
        Vector2 size = target.rect.size;
        float slackX = Mathf.Max(0f, size.x * (scale - 1f) * 0.5f);
        float slackY = Mathf.Max(0f, size.y * (scale - 1f) * 0.5f);
        offset.x = Mathf.Clamp(offset.x, -slackX, slackX);
        offset.y = Mathf.Clamp(offset.y, -slackY, slackY);
        return offset;
    }
}
