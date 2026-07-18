using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Riftborn.Tutorial
{
    // One highlight the director asks HighlightSystem to draw this frame. The
    // director resolves a step's HighlightTarget (portal / named anchor) to the
    // world Transform here; HighlightSystem re-projects it every frame.
    public struct HighlightRequest
    {
        public Transform Anchor;   // world target the ring hugs / the arrow points at
        public bool ShowRing;
        public bool ShowArrow;
        public float ArrowClock;   // 12/0 = top, 3 = right, 6 = bottom, 9 = left
        public float WorldRadius;  // ring size in world units; <= 0 → system default
    }

    // Screen-space highlights anchored to world targets (portals, board tokens,
    // named TutorialAnchor markers): each is a pulsing ring hugging its target
    // plus a bobbing arrow parked at a clock position around the ring, optionally
    // dimming the rest of the screen with a four-rect frame that leaves a hole
    // around all of them. Everything re-projects every frame, so highlights track
    // camera tweens and moving targets for free.
    //
    // A step can show several highlights at once, so ring/arrow pairs live in a
    // pool (grown on demand, hidden when unused) that all share two code-generated
    // sprites. Custom Graphic subclasses render nothing in this project's canvas,
    // while Images render fine — so the whole thing rides the Image path.
    // Grey-box; art pass in M7.
    public class HighlightSystem : MonoBehaviour
    {
        [Header("Anchor")]
        [Tooltip("Default world-space radius the ring hugs (a portal is ~2 units). A highlight can override this per target.")]
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

        // One pooled ring+arrow pair, reassigned to a different target each step.
        private class HighlightView
        {
            public Image ring;
            public Image arrow;
            public Transform anchor;
            public bool showRing;
            public bool showArrow;
            public float clock;
            public float worldRadius;
        }

        private RectTransform layer;
        private readonly Image[] dimRects = new Image[4];
        private readonly List<HighlightView> views = new();
        private Sprite ringSprite;
        private Sprite arrowSprite;

        private int activeCount;
        private bool dim;
        private Camera cam;

        private void Awake()
        {
            layer = TutorialCanvas.GetLayer("Highlight", 0);

            // Dim rects first: siblings draw in order, so the pooled ring/arrow
            // Images (created later, on demand) land on top of the dim frame.
            for (int i = 0; i < dimRects.Length; i++)
            {
                dimRects[i] = CreateImage($"Dim{i}", null, Vector2.zero, new Color(0f, 0f, 0f, dimAlpha));
            }

            ringSprite = BuildRingSprite();
            arrowSprite = BuildArrowSprite();
        }

        private void OnDestroy()
        {
            DestroySprite(ref ringSprite);
            DestroySprite(ref arrowSprite);
        }

        // Show exactly these highlights this step (an empty/null list clears).
        // The director owns resolving targets to Transforms; we just track them.
        public void Show(IReadOnlyList<HighlightRequest> requests, bool dimBackground)
        {
            dim = dimBackground;
            activeCount = requests?.Count ?? 0;
            EnsureViews(activeCount);

            for (int i = 0; i < activeCount; i++)
            {
                HighlightRequest r = requests[i];
                HighlightView v = views[i];
                v.anchor = r.Anchor;
                v.showRing = r.ShowRing;
                v.showArrow = r.ShowArrow;
                v.clock = r.ArrowClock;
                v.worldRadius = r.WorldRadius;
            }

            for (int i = activeCount; i < views.Count; i++) HideView(views[i]);
            if (activeCount == 0) SetDimActive(false);
        }

        public void Clear()
        {
            activeCount = 0;
            for (int i = 0; i < views.Count; i++) HideView(views[i]);
            SetDimActive(false);
        }

        private void LateUpdate()
        {
            if (activeCount == 0)
            {
                SetDimActive(false);
                return;
            }

            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            float pulse = 1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed);
            float bob = (Mathf.Sin(Time.time * bobSpeed) * 0.5f + 0.5f) * bobAmount;

            bool anyHole = false;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < activeCount; i++)
            {
                HighlightView v = views[i];
                if (v.anchor == null) { HideView(v); continue; }

                Vector3 screen = cam.WorldToScreenPoint(v.anchor.position);
                if (screen.z < 0f) { HideView(v); continue; } // behind the camera — be safe

                RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out Vector2 local);

                // Project a world-space radius so the ring hugs the target at any zoom.
                float wr = v.worldRadius > 0f ? v.worldRadius : worldRadius;
                Vector3 screenEdge = cam.WorldToScreenPoint(v.anchor.position + cam.transform.right * wr);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenEdge, null, out Vector2 localEdge);
                float radius = Mathf.Clamp((localEdge - local).magnitude, minCanvasRadius, maxCanvasRadius);

                if (v.showRing)
                {
                    var ringRect = (RectTransform)v.ring.transform;
                    ringRect.anchoredPosition = local;
                    ringRect.localScale = Vector3.one * (radius / RingBaseRadius * pulse);
                }
                v.ring.gameObject.SetActive(v.showRing);

                if (v.showArrow)
                {
                    // Clock hour → angle clockwise from the top; the arrow sits on
                    // that side of the ring and rotates so its apex points inward.
                    float phi = Mathf.Repeat(v.clock, 12f) / 12f * Mathf.PI * 2f;
                    Vector2 dir = new Vector2(Mathf.Sin(phi), Mathf.Cos(phi));
                    var arrowRect = (RectTransform)v.arrow.transform;
                    float dist = radius * pulse + ArrowGap + arrowRect.sizeDelta.y * 0.5f + bob;
                    arrowRect.anchoredPosition = local + dir * dist;
                    arrowRect.localEulerAngles = new Vector3(0f, 0f, -phi * Mathf.Rad2Deg);
                }
                v.arrow.gameObject.SetActive(v.showArrow);

                // Every active target punches a dim hole, even parts-off spotlights.
                float holeR = radius * 1.35f + 10f;
                minX = Mathf.Min(minX, local.x - holeR); maxX = Mathf.Max(maxX, local.x + holeR);
                minY = Mathf.Min(minY, local.y - holeR); maxY = Mathf.Max(maxY, local.y + holeR);
                anyHole = true;
            }

            if (dim && anyHole)
            {
                var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
                UpdateDim(center, (maxX - minX) * 0.5f, (maxY - minY) * 0.5f);
                SetDimActive(true);
            }
            else
            {
                SetDimActive(false);
            }
        }

        private void EnsureViews(int count)
        {
            while (views.Count < count)
            {
                views.Add(new HighlightView
                {
                    ring = CreateImage($"Ring{views.Count}", ringSprite, new Vector2(RingRectSize, RingRectSize), ringColor),
                    arrow = CreateImage($"Arrow{views.Count}", arrowSprite, new Vector2(56f, 42f), arrowColor),
                });
            }
        }

        private static void HideView(HighlightView v)
        {
            if (v.ring.gameObject.activeSelf) v.ring.gameObject.SetActive(false);
            if (v.arrow.gameObject.activeSelf) v.arrow.gameObject.SetActive(false);
        }

        private void SetDimActive(bool on)
        {
            foreach (Image dimRect in dimRects)
                if (dimRect.gameObject.activeSelf != on) dimRect.gameObject.SetActive(on);
        }

        // Four opaque rects tile the screen around a rectangular hole (the union
        // AABB of every active highlight) — a poor man's cutout dim with plain
        // Images, no shader. Multiple separate holes aren't possible this way, so
        // several highlights share one hole spanning all of them.
        private void UpdateDim(Vector2 center, float halfHoleW, float halfHoleH)
        {
            Rect r = layer.rect;
            float halfW = r.width * 0.5f;
            float halfH = r.height * 0.5f;

            float left = Mathf.Clamp(center.x - halfHoleW, -halfW, halfW);
            float right = Mathf.Clamp(center.x + halfHoleW, -halfW, halfW);
            float bottom = Mathf.Clamp(center.y - halfHoleH, -halfH, halfH);
            float top = Mathf.Clamp(center.y + halfHoleH, -halfH, halfH);

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

        // ── Generated sprites (shared across all pooled views) ────────────────

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

        // White triangle, apex at the bottom — the "look here" arrow. At rest
        // (clock 12) it hovers above the ring pointing down; the LateUpdate math
        // rotates it to point inward from any clock position.
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
