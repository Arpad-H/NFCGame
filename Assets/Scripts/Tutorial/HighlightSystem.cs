using UnityEngine;
using UnityEngine.UI;

namespace Riftborn.Tutorial
{
    // Screen-space highlight anchored to a world target (a portal, a board
    // token): a pulsing ring hugging the target plus a bobbing arrow above it,
    // optionally dimming the rest of the screen with a four-rect frame that
    // leaves a hole around the target. Everything is re-projected every frame,
    // so it tracks camera tweens and moving targets for free.
    //
    // The ring and arrow are plain Images with sprites generated in code
    // (custom Graphic subclasses built their mesh but rendered nothing in this
    // project's canvas setup, while Images render fine — so the whole
    // highlight rides the Image path). Grey-box; art pass in M7.
    public class HighlightSystem : MonoBehaviour
    {
        [Header("Anchor")]
        [Tooltip("World-space radius the ring hugs around the target (a portal is ~2 units).")]
        public float worldRadius = 2.2f;
        [Tooltip("Smallest the ring may shrink to on screen, in canvas units.")]
        public float minCanvasRadius = 46f;
        [Tooltip("Largest the ring may grow to on screen, in canvas units.")]
        public float maxCanvasRadius = 320f;

        [Header("Look")]
        public Color ringColor = new Color(1f, 0.84f, 0.29f, 0.9f);
        public Color arrowColor = new Color(1f, 0.84f, 0.29f, 1f);
        [Range(0f, 1f)] public float dimAlpha = 0.55f;
        public float pulseAmount = 0.07f;
        public float pulseSpeed = 3.4f;
        public float bobAmount = 12f;
        public float bobSpeed = 3f;

        // Ring sprite geometry: drawn into a RingTexSize texture with the band's
        // outer edge at RingTexOuter px from center, shown in a RingRectSize rect.
        private const int RingTexSize = 256;
        private const float RingTexOuter = 120f;
        private const float RingTexThickness = 16f;
        private const float RingRectSize = 200f;
        // The drawn outer radius in canvas units at localScale 1 — the anchor
        // math scales the transform so this matches the projected world radius.
        private const float RingBaseRadius = RingTexOuter / RingTexSize * RingRectSize;

        private const float ArrowGap = 14f; // gap between ring edge and arrow tip

        private RectTransform layer;
        private Image ring;
        private Image arrow;
        private readonly Image[] dimRects = new Image[4];
        private Sprite ringSprite;
        private Sprite arrowSprite;

        private Transform target;
        private bool dim;
        private Camera cam;

        private void Awake()
        {
            layer = TutorialCanvas.GetLayer("Highlight", 0);

            // Dim rects first: siblings draw in order, the ring/arrow go on top.
            for (int i = 0; i < dimRects.Length; i++)
            {
                dimRects[i] = CreateImage($"Dim{i}", null, Vector2.zero, new Color(0f, 0f, 0f, dimAlpha));
            }

            ringSprite = BuildRingSprite();
            ring = CreateImage("Ring", ringSprite, new Vector2(RingRectSize, RingRectSize), ringColor);

            arrowSprite = BuildArrowSprite();
            arrow = CreateImage("Arrow", arrowSprite, new Vector2(56f, 42f), arrowColor);
        }

        private void OnDestroy()
        {
            DestroySprite(ref ringSprite);
            DestroySprite(ref arrowSprite);
        }

        public void Show(Transform anchor, bool dimBackground)
        {
            target = anchor;
            dim = dimBackground;
        }

        public void Clear()
        {
            target = null;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                if (ring.gameObject.activeSelf) SetVisible(false);
                return;
            }

            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(target.position);
            if (screen.z < 0f) // behind the camera — impossible top-down, but be safe
            {
                SetVisible(false);
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out Vector2 local);

            // Project a world-space radius so the ring hugs the target at any zoom.
            Vector3 screenEdge = cam.WorldToScreenPoint(target.position + cam.transform.right * worldRadius);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenEdge, null, out Vector2 localEdge);
            float radius = Mathf.Clamp((localEdge - local).magnitude, minCanvasRadius, maxCanvasRadius);

            float pulse = 1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed);
            var ringRect = (RectTransform)ring.transform;
            ringRect.anchoredPosition = local;
            ringRect.localScale = Vector3.one * (radius / RingBaseRadius * pulse);

            float bob = (Mathf.Sin(Time.time * bobSpeed) * 0.5f + 0.5f) * bobAmount;
            var arrowRect = (RectTransform)arrow.transform;
            arrowRect.anchoredPosition =
                local + new Vector2(0f, radius * pulse + ArrowGap + arrowRect.sizeDelta.y * 0.5f + bob);

            if (dim) UpdateDim(local, radius * 1.35f + 10f);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            ring.gameObject.SetActive(visible);
            arrow.gameObject.SetActive(visible);
            bool showDim = visible && dim;
            foreach (Image dimRect in dimRects) dimRect.gameObject.SetActive(showDim);
        }

        // Four opaque rects tile the screen around a square hole centred on the
        // target — a poor man's cutout dim with plain Images, no shader.
        private void UpdateDim(Vector2 center, float holeRadius)
        {
            Rect r = layer.rect;
            float halfW = r.width * 0.5f;
            float halfH = r.height * 0.5f;

            float left = Mathf.Clamp(center.x - holeRadius, -halfW, halfW);
            float right = Mathf.Clamp(center.x + holeRadius, -halfW, halfW);
            float bottom = Mathf.Clamp(center.y - holeRadius, -halfH, halfH);
            float top = Mathf.Clamp(center.y + holeRadius, -halfH, halfH);

            SetRect(dimRects[0], new Vector2(0f, (top + halfH) * 0.5f), new Vector2(r.width, halfH - top));
            SetRect(dimRects[1], new Vector2(0f, (bottom - halfH) * 0.5f), new Vector2(r.width, bottom + halfH));
            SetRect(dimRects[2], new Vector2((left - halfW) * 0.5f, (top + bottom) * 0.5f),
                new Vector2(left + halfW, top - bottom));
            SetRect(dimRects[3], new Vector2((right + halfW) * 0.5f, (top + bottom) * 0.5f),
                new Vector2(halfW - right, top - bottom));
        }

        private static void SetRect(Image image, Vector2 center, Vector2 size)
        {
            var rect = (RectTransform)image.transform;
            rect.anchoredPosition = center;
            rect.sizeDelta = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
        }

        private Image CreateImage(string name, Sprite sprite, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(layer, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            go.SetActive(false);
            return image;
        }

        // ── Generated sprites ────────────────────────────────────────────────

        // White annulus with ~2px soft edges; Image.color tints it.
        private static Sprite BuildRingSprite()
        {
            const float soft = 2f;
            var pixels = new Color32[RingTexSize * RingTexSize];
            float c = (RingTexSize - 1) * 0.5f;

            for (int y = 0; y < RingTexSize; y++)
            {
                for (int x = 0; x < RingTexSize; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    // Distance into the ring band; positive = inside the band.
                    float inBand = Mathf.Min(RingTexOuter - d, d - (RingTexOuter - RingTexThickness));
                    byte a = (byte)(Mathf.Clamp01(inBand / soft + 0.5f) * 255f);
                    pixels[y * RingTexSize + x] = new Color32(255, 255, 255, a);
                }
            }

            return SpriteFromPixels(pixels, RingTexSize, RingTexSize);
        }

        // White triangle, apex at the bottom — the "look here" arrow that
        // hovers above the ring pointing down at the target.
        private static Sprite BuildArrowSprite()
        {
            const int w = 128;
            const int h = 96;
            const float soft = 2f;
            var pixels = new Color32[w * h];
            float cx = (w - 1) * 0.5f;

            for (int y = 0; y < h; y++)
            {
                // Row 0 is the bottom (the apex); the triangle widens upward.
                float halfWidth = y / (float)(h - 1) * (w * 0.5f - 2f);
                for (int x = 0; x < w; x++)
                {
                    byte a = (byte)(Mathf.Clamp01((halfWidth - Mathf.Abs(x - cx)) / soft + 0.5f) * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }

            return SpriteFromPixels(pixels, w, h);
        }

        private static Sprite SpriteFromPixels(Color32[] pixels, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static void DestroySprite(ref Sprite sprite)
        {
            if (sprite == null) return;
            Texture2D tex = sprite.texture;
            Destroy(sprite);
            if (tex != null) Destroy(tex);
            sprite = null;
        }
    }
}
