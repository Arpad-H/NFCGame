using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "GameObject ▸ UI ▸ Vignette" — builds a ready-to-use vignette overlay so you never have to
/// wire the canvas by hand. It creates its own Screen Space - Overlay canvas at a high sort order
/// (so the vignette sits above ordinary UI) with a full-screen stretched <see cref="UIVignette"/>.
///
/// Make it a prefab once (drag the created object into your Project) and drop that prefab into the
/// main menu and the game scene. If you'd rather layer it into an existing canvas instead, just add
/// a full-screen UI object with the UIVignette component to that canvas.
/// </summary>
static class UIVignetteMenu
{
    [MenuItem("GameObject/UI/Vignette", false, 2100)]
    static void CreateVignette(MenuCommand command)
    {
        var canvasGo = new GameObject("VignetteCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // above typical gameplay/menu UI; lower if it should sit under something

        var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;   // the overlay is purely visual — let clicks pass through

        var vignetteGo = new GameObject("Vignette", typeof(UIVignette));
        var rect = vignetteGo.GetComponent<RectTransform>();
        rect.SetParent(canvasGo.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // new GameObject(...) doesn't fire Reset(), so the inherited Graphic.color would default
        // to white. Set the intended black tint explicitly so the overlay is visible immediately.
        vignetteGo.GetComponent<UIVignette>().color = Color.black;

        // Parent under the context object (e.g. a right-clicked canvas) if there is one.
        GameObjectUtility.SetParentAndAlign(canvasGo, command.context as GameObject);

        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Vignette");
        Selection.activeGameObject = vignetteGo;
    }
}
