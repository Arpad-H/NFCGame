using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riftborn.Tutorial
{
    // The tutorial's primary prompt UI: a persistent bottom-center panel with
    // the current step's instruction, plus a short-lived top-center toast for
    // off-script rejections. Self-bootstrapping grey-box (Announcer pattern):
    // everything is built in code on the shared tutorial canvas. Pointing at
    // board targets is HighlightSystem's job — one arrow per step, not two.
    public class NotificationView : MonoBehaviour
    {
        [Header("Prompt panel")]
        [Tooltip("Panel width in canvas units (1920x1080 reference).")]
        public float panelWidth = 860f;
        [Tooltip("Gap between the panel and the bottom screen edge.")]
        public float bottomMargin = 48f;

        [Header("Rejection toast")]
        [Tooltip("Seconds a rejection toast stays readable before fading.")]
        public float toastSeconds = 3f;

        private RectTransform panel;
        private CanvasGroup panelGroup;
        private TMP_Text panelLabel;
        private Coroutine panelRoutine;

        private RectTransform toast;
        private CanvasGroup toastGroup;
        private TMP_Text toastLabel;
        private Coroutine toastRoutine;

        private void Awake()
        {
            RectTransform layer = TutorialCanvas.GetLayer("Notification", 10);

            (panel, panelGroup, panelLabel) = BuildBox(layer, "Prompt",
                panelWidth, new Color(0.08f, 0.09f, 0.12f, 0.94f), 30f);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, bottomMargin);
            panel.gameObject.SetActive(false);

            (toast, toastGroup, toastLabel) = BuildBox(layer, "RejectionToast",
                700f, new Color(0.45f, 0.10f, 0.10f, 0.95f), 26f);
            toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 1f);
            toast.pivot = new Vector2(0.5f, 1f);
            toast.anchoredPosition = new Vector2(0f, -120f);
            toast.gameObject.SetActive(false);

            BuildSkipButton();
        }

        // An always-on "leave the tutorial" button in the top-right corner. It
        // marks the tutorial seen (so a menu won't force it again) and returns
        // to the menu. The only raycast-taking graphic on the tutorial canvas.
        private void BuildSkipButton()
        {
            RectTransform layer = TutorialCanvas.GetLayer("Skip", 20);

            var go = new GameObject("SkipButton", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-28f, -28f);
            rt.sizeDelta = new Vector2(210f, 56f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.10f, 0.11f, 0.14f, 0.85f);
            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                TutorialState.MarkComplete();
                TutorialLauncher.ReturnToMenu();
            });

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rt, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "Skip Tutorial";
            label.fontSize = 24f;
            label.color = new Color(0.85f, 0.86f, 0.9f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        public void Show(string body)
        {
            panelLabel.text = body;
            panel.gameObject.SetActive(true);
            if (panelRoutine != null) StopCoroutine(panelRoutine);
            panelRoutine = StartCoroutine(PopIn(panel, panelGroup));
        }

        public void Hide()
        {
            if (panelRoutine != null)
            {
                StopCoroutine(panelRoutine);
                panelRoutine = null;
            }

            panel.gameObject.SetActive(false);
        }

        // Off-script play feedback. Independent of the step prompt, so the
        // instruction stays on screen while the correction comes and goes.
        public void ShowToast(string message)
        {
            toastLabel.text = message;
            toast.gameObject.SetActive(true);
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            yield return PopIn(toast, toastGroup);
            yield return new WaitForSeconds(toastSeconds);

            const float fadeSeconds = 0.25f;
            float t = 0f;
            while (t < fadeSeconds)
            {
                toastGroup.alpha = 1f - t / fadeSeconds;
                t += Time.deltaTime;
                yield return null;
            }

            toast.gameObject.SetActive(false);
            toastRoutine = null;
        }

        private static IEnumerator PopIn(RectTransform rt, CanvasGroup group)
        {
            const float duration = 0.18f;
            float t = 0f;
            while (t < duration)
            {
                float p = t / duration;
                group.alpha = p;
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, EaseOutBack(p));
                t += Time.deltaTime;
                yield return null;
            }

            group.alpha = 1f;
            rt.localScale = Vector3.one;
        }

        // Same ease the Announcer uses, so the tutorial pops feel related.
        private static float EaseOutBack(float p)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = p - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        // A dark box that grows vertically to fit its text: Image + vertical
        // layout + fitter with a wrapped TMP label inside. Anchoring is the
        // caller's business.
        private static (RectTransform rect, CanvasGroup group, TMP_Text label) BuildBox(
            RectTransform parent, string name, float width, Color background, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(width, 100f);

            var image = go.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 16, 18);
            layout.childAlignment = TextAnchor.MiddleCenter;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rt, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            return (rt, group, label);
        }
    }
}
