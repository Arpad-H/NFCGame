using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Riftborn.Tutorial
{
    // The one screen-space canvas all tutorial UI draws on, created on demand
    // (Announcer pattern: grey-box built from code, nothing to wire in the
    // scene; the M7 art pass restyles it). Views ask for a named full-screen
    // layer; layers stay sorted by the order they were requested with, so draw
    // order is stable no matter which component's Awake ran first.
    internal static class TutorialCanvas
    {
        // Above the game HUD; the IMGUI debug overlay still draws over everything.
        private const int SortingOrder = 400;

        private static Canvas canvas;
        private static readonly Dictionary<RectTransform, int> layerOrders = new();

        public static RectTransform GetLayer(string name, int order)
        {
            EnsureCanvas();

            var go = new GameObject(name, typeof(RectTransform));
            var layer = (RectTransform)go.transform;
            layer.SetParent(canvas.transform, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;

            layerOrders[layer] = order;
            SortLayers();
            return layer;
        }

        private static void EnsureCanvas()
        {
            if (canvas != null) return; // Unity-null: recreates after a scene reload

            var go = new GameObject("TutorialCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // A raycaster so the Skip button can be clicked. Board input is
            // WebSocket, not screen-space, so this can't eat card plays; and
            // every other tutorial graphic sets raycastTarget=false, so clicks
            // anywhere but the Skip button fall straight through to the game UI.
            go.AddComponent<GraphicRaycaster>();
        }

        private static void SortLayers()
        {
            // The static map outlives scene reloads while the layers don't —
            // prune destroyed entries before ordering the survivors.
            var alive = new List<KeyValuePair<RectTransform, int>>();
            foreach (KeyValuePair<RectTransform, int> pair in layerOrders)
                if (pair.Key != null)
                    alive.Add(pair);

            layerOrders.Clear();
            foreach (KeyValuePair<RectTransform, int> pair in alive) layerOrders[pair.Key] = pair.Value;

            alive.Sort((a, b) => a.Value.CompareTo(b.Value));
            for (int i = 0; i < alive.Count; i++) alive[i].Key.SetSiblingIndex(i);
        }
    }
}
