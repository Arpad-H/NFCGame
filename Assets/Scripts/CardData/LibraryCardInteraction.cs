using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Per-library-card behaviour, added at runtime by LibraryManager to every card it
// spawns into the grid. It handles the two things local to a single card:
//   * hover  — fades in an outline around the card and plays a short blip;
//   * right-click — asks the shared LibraryCardFocusController to fly this card to
//     the centre of the screen.
// Everything after that (the fly, the blur, the click-outside-to-return) is the
// controller's job, since only one card is ever focused at a time.
//
// The card is a world-space transform rig: a plain Transform root (this object, also
// holding the CardVisualizer) whose graphics live under a nested Canvas child. So we
// work with the root as a plain Transform, lift sorting through that existing Canvas,
// and hang the hover outline inside the Canvas rect where it can actually size itself
// to the card.
[DisallowMultipleComponent]
public class LibraryCardInteraction : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    LibraryCardFocusController _controller;
    Transform _card;
    Canvas _cardCanvas;
    Image _outline;
    Tween _outlineTween;

    // The card root (a plain Transform) and its own Canvas — used by the controller
    // to move/scale the card and to lift it above the blur while focused.
    public Transform Card => _card;
    public Canvas CardCanvas => _cardCanvas;

    public void Init(LibraryCardFocusController controller)
    {
        _controller = controller;
        _card = transform;
        _cardCanvas = GetComponentInChildren<Canvas>(true);
        CreateOutline();
    }

    // A dim-to-invisible border image behind the card face. It lives inside the card's
    // Canvas (the only RectTransform here with a real rect to stretch against), padded
    // outward so it reads as a ring around the card once it fades in.
    void CreateOutline()
    {
        if (_controller == null || _controller.OutlineSprite == null) return;
        if (_cardCanvas == null) return;

        var canvasRect = (RectTransform)_cardCanvas.transform;

        var go = new GameObject("HoverOutline", typeof(RectTransform), typeof(Image));
        go.layer = _cardCanvas.gameObject.layer;
        var ort = (RectTransform)go.transform;
        ort.SetParent(canvasRect, false);
        ort.SetAsFirstSibling(); // draw behind the card face -> only the padded ring shows

        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        float p = _controller.OutlinePadding;
        ort.offsetMin = new Vector2(-p, -p);
        ort.offsetMax = new Vector2(p, p);
        ort.localScale = Vector3.one;
        ort.localRotation = Quaternion.identity;

        _outline = go.GetComponent<Image>();
        _outline.sprite = _controller.OutlineSprite;
        _outline.type = _controller.OutlineNineSliced ? Image.Type.Sliced : Image.Type.Simple;
        _outline.raycastTarget = false;

        Color c = _controller.OutlineColor;
        c.a = 0f;
        _outline.color = c;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_controller == null || _controller.HasFocus) return;
        _controller.PlayHover();
        FadeOutline(_controller.OutlineColor.a);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        FadeOutline(0f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_controller == null) return;
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (_controller.HasFocus) return; // already focused; ignore clicks on the card

        HideOutline();
        _controller.Focus(this);
    }

    // Snap the outline hidden with no fade — used when the card is about to fly out.
    public void HideOutline()
    {
        if (_outline == null) return;
        _outlineTween?.Kill();
        Color c = _outline.color;
        c.a = 0f;
        _outline.color = c;
    }

    void FadeOutline(float targetAlpha)
    {
        if (_outline == null) return;
        _outlineTween?.Kill();
        _outlineTween = _outline.DOFade(targetAlpha, _controller.OutlineFadeDuration).SetUpdate(true);
    }
}
