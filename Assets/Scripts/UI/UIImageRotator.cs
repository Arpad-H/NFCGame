using UnityEngine;

/// <summary>
/// Continuously rotates a UI element (e.g. a ring or icon Image) in place around its
/// true visual center, even when that center isn't exactly at the RectTransform's pivot
/// (off-center circle art otherwise wobbles/orbits instead of spinning in place).
/// Attach directly to the RectTransform you want spinning.
/// </summary>
public class UIImageRotator : MonoBehaviour
{
    [Tooltip("Degrees per second. Positive = clockwise, negative = counter-clockwise.")]
    [SerializeField] float speed = 30f;

    [Tooltip("Ignores Time.timeScale (keeps spinning while the game is paused).")]
    [SerializeField] bool useUnscaledTime = true;

    [Tooltip("Pause rotation entirely.")]
    [SerializeField] bool isRotating = true;

    [Header("Center Correction")]
    [Tooltip("Offset from this RectTransform's pivot to the image's true visual center, " +
             "in local pixels (unrotated). If the circle wobbles or orbits instead of " +
             "spinning in place, nudge this until the yellow gizmo sits on the visual " +
             "center of the art.")]
    [SerializeField] Vector2 centerOffset = Vector2.zero;

    RectTransform _rect;
    Vector3 _baseLocalPos;
    Vector2 _trueCenter;
    float _angle;

    void Awake()
    {
        _rect = transform as RectTransform;
        _angle = transform.localEulerAngles.z;

        Vector2 basePivotPos;
        if (_rect != null) basePivotPos = _rect.anchoredPosition;
        else basePivotPos = _baseLocalPos = transform.localPosition;

        _trueCenter = basePivotPos + RotateVector(centerOffset, _angle);
    }

    void Update()
    {
        if (!isRotating) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _angle -= speed * dt;

        Vector2 pivotPos = _trueCenter - RotateVector(centerOffset, _angle);

        if (_rect != null) _rect.anchoredPosition = pivotPos;
        else transform.localPosition = new Vector3(pivotPos.x, pivotPos.y, _baseLocalPos.z);

        transform.localRotation = Quaternion.Euler(0f, 0f, _angle);
    }

    static Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    void OnDrawGizmosSelected()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;

        Vector3 worldCenter = rt.TransformPoint(centerOffset);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(rt.position, worldCenter);
        Gizmos.DrawWireSphere(worldCenter, 4f);
    }
}
