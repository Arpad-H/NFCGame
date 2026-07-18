using System.Collections;
using UnityEngine;

namespace Riftborn.Tutorial
{
    // Framing presets over the 3-lane board. "FullBoard" is the authored scene
    // pose, captured at startup; "SingleLane" centres on a lane's two portals
    // and tightens the orthographic size so one duel fills the screen. Tweens
    // write the transform absolutely each frame — the camera's StressReceiver
    // shake composes additively on top and cleans itself up, so they don't
    // fight.
    public class TutorialCamera : MonoBehaviour
    {
        [Tooltip("Seconds a framing tween takes.")]
        public float tweenSeconds = 0.9f;
        [Tooltip("Half-width (world X) a framed lane must show — portal plus full card stacks.")]
        public float laneHalfWidth = 8f;
        [Tooltip("Half-depth (world Z) a framed lane must show.")]
        public float laneHalfDepth = 4.5f;

        private Camera cam;
        private Vector3 homePosition;
        private float homeSize;
        private Coroutine tween;

        private void Awake()
        {
            cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[Tutorial] No camera in scene — TutorialCamera disabled.");
                enabled = false;
                return;
            }

            homePosition = cam.transform.position;
            // Meaningful even on a perspective camera: it is only our zoom reference.
            homeSize = cam.orthographicSize;
        }

        public void FrameFullBoard()
        {
            StartTween(homePosition, homeSize);
        }

        // Tween to an explicit authored pose: a world position and orthographic
        // size (half the view height). The camera keeps its current rotation — the
        // board is viewed top-down — so a Custom step only picks where to look and
        // how far to zoom. Used by CameraShot.Custom.
        public void MoveTo(Vector3 position, float orthoSize)
        {
            if (!enabled) return;
            StartTween(position, Mathf.Max(0.01f, orthoSize));
        }

        public void FrameLane(int laneIndex)
        {
            if (!enabled) return;

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Portal portal in FindObjectsByType<Portal>(FindObjectsSortMode.None))
            {
                if (portal.laneIndex != laneIndex) continue;
                sum += portal.transform.position;
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning($"[Tutorial] No portals found for lane {laneIndex} — camera stays put.");
                return;
            }

            Vector3 center = sum / count;
            float size = Mathf.Max(laneHalfDepth, laneHalfWidth / Mathf.Max(0.1f, cam.aspect));
            StartTween(new Vector3(center.x, homePosition.y, center.z), size);
        }

        private void StartTween(Vector3 toPosition, float toSize)
        {
            if (!enabled) return;

            // Perspective fallback (the shipped camera is orthographic, looking
            // straight down): approximate the zoom by scaling the camera height.
            if (!cam.orthographic)
                toPosition.y = homePosition.y * (toSize / Mathf.Max(0.01f, homeSize));

            if (tween != null) StopCoroutine(tween);
            tween = StartCoroutine(TweenRoutine(toPosition, toSize));
        }

        private IEnumerator TweenRoutine(Vector3 toPosition, float toSize)
        {
            Transform t = cam.transform;
            Vector3 fromPosition = t.position;
            float fromSize = cam.orthographicSize;
            float duration = Mathf.Max(0.01f, tweenSeconds);

            float e = 0f;
            while (e < duration)
            {
                float p = EaseInOutCubic(e / duration);
                t.position = Vector3.LerpUnclamped(fromPosition, toPosition, p);
                if (cam.orthographic) cam.orthographicSize = Mathf.LerpUnclamped(fromSize, toSize, p);
                e += Time.deltaTime;
                yield return null;
            }

            t.position = toPosition;
            if (cam.orthographic) cam.orthographicSize = toSize;
            tween = null;
        }

        private static float EaseInOutCubic(float p)
        {
            return p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) * 0.5f;
        }
    }
}
