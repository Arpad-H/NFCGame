using UnityEngine;

public class StressReceiver : MonoBehaviour
{
    private float _trauma;
    // The shake offsets we are currently adding on top of the transform, kept so
    // we can subtract *exactly* what we added and land back on the original pose.
    // Rotation is stored as a quaternion delta: composing and removing it with
    // quaternion multiplication is exact, whereas the old euler round-trip
    // (Quaternion.Euler(localRotation.eulerAngles +/- delta)) re-canonicalised the
    // angles every frame, accumulated error, and left the camera slightly rotated
    // once the shake ended — which on a top-down camera reads as a reframing.
    private Vector3 _appliedPosition = Vector3.zero;
    private Quaternion _appliedRotation = Quaternion.identity;
    [Tooltip("Exponent for calculating the shake factor. Useful for creating different effect fade outs")]
    public float TraumaExponent = 1;
    [Tooltip("Maximum angle that the gameobject can shake. In euler angles.")]
    public Vector3 MaximumAngularShake = Vector3.one * 5;
    [Tooltip("Maximum translation that the gameobject can receive when applying the shake effect.")]
    public Vector3 MaximumTranslationShake = Vector3.one * .75f;

    private void Update()
    {
        float shake = Mathf.Pow(_trauma, TraumaExponent);
        /* Only apply this when there is active trauma */
        if(shake > 0)
        {
            Vector3 nextPosition = new Vector3(
                MaximumTranslationShake.x * (Mathf.PerlinNoise(0, Time.time * 25) * 2 - 1),
                MaximumTranslationShake.y * (Mathf.PerlinNoise(1, Time.time * 25) * 2 - 1),
                MaximumTranslationShake.z * (Mathf.PerlinNoise(2, Time.time * 25) * 2 - 1)
            ) * shake;

            Quaternion nextRotation = Quaternion.Euler(new Vector3(
                MaximumAngularShake.x * (Mathf.PerlinNoise(3, Time.time * 25) * 2 - 1),
                MaximumAngularShake.y * (Mathf.PerlinNoise(4, Time.time * 25) * 2 - 1),
                MaximumAngularShake.z * (Mathf.PerlinNoise(5, Time.time * 25) * 2 - 1)
            ) * shake);

            /* Swap last frame's offset for this frame's, so anything else that moved
               the transform is left untouched and the shake stays purely additive. */
            transform.localPosition += nextPosition - _appliedPosition;
            transform.localRotation = transform.localRotation * Quaternion.Inverse(_appliedRotation) * nextRotation;

            _appliedPosition = nextPosition;
            _appliedRotation = nextRotation;
            _trauma = Mathf.Clamp01(_trauma - Time.deltaTime);
        }
        else
        {
            if (_appliedPosition == Vector3.zero && _appliedRotation == Quaternion.identity) return;
            /* Remove exactly the offset we last applied, restoring the original pose. */
            transform.localPosition -= _appliedPosition;
            transform.localRotation = transform.localRotation * Quaternion.Inverse(_appliedRotation);
            _appliedPosition = Vector3.zero;
            _appliedRotation = Quaternion.identity;
        }
    }

    /// <summary>
    ///  Applies a stress value to the current object.
    /// </summary>
    /// <param name="Stress">[0,1] Amount of stress to apply to the object</param>
    public void InduceStress(float Stress)
    {
        _trauma = Mathf.Clamp01(_trauma + Stress);
    }
}
