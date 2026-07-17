using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Per-library-card behaviour, added at runtime by LibraryManager to every card it
// spawns into the grid. It handles the two things local to a single card:
//   * hover  — ramps up the card's resonance glow and plays a short blip;
//   * right-click — asks the shared LibraryCardFocusController to fly this card to
//     the centre of the screen.
// Everything after that (the fly, the blur, the click-outside-to-return) is the
// controller's job, since only one card is ever focused at a time.
//
// The glow is the "Glow" Image authored into the card prefab, behind the card face and
// already tinted to the card's resonance by CardVisualizer. It ships disabled — the
// board and the exporter show plain cards — so switching it on here is what makes the
// glow a library-only effect.
//
// The card is a world-space transform rig: a plain Transform root (this object, also
// holding the CardVisualizer) whose graphics live under a nested Canvas child. So we
// work with the root as a plain Transform and lift sorting through that existing Canvas.
[DisallowMultipleComponent]
public class LibraryCardInteraction : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    LibraryCardFocusController _controller;
    Transform _card;
    Canvas _cardCanvas;
    Image _glow;
    Tween _glowTween;

    // The card root (a plain Transform) and its own Canvas — used by the controller
    // to move/scale the card and to lift it above the blur while focused.
    public Transform Card => _card;
    public Canvas CardCanvas => _cardCanvas;

    public void Init(LibraryCardFocusController controller)
    {
        _controller = controller;
        _card = transform;
        _cardCanvas = GetComponentInChildren<Canvas>(true);
        SetupGlow();
    }

    // Wake the prefab's glow for this library card. CardVisualizer has already tinted it
    // to the resonance and zeroed its alpha, so enabling it here shows nothing until a
    // hover ramps it up.
    void SetupGlow()
    {
        var visualizer = GetComponent<CardVisualizer>();
        _glow = visualizer != null ? visualizer.resonanceGlow : null;
        if (_glow == null) return;

        _glow.gameObject.SetActive(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_controller == null || _controller.HasFocus) return;
        _controller.PlayHover();
        FadeGlow(_controller.GlowAlpha, _controller.GlowFadeInDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_controller == null) return;
        FadeGlow(0f, _controller.GlowFadeOutDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_controller == null) return;
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (_controller.HasFocus) return; // already focused; ignore clicks on the card

        HideGlow();
        _controller.Focus(this);
    }

    // Snap the glow off with no fade — used when the card is about to fly out.
    public void HideGlow()
    {
        if (_glow == null) return;
        _glowTween?.Kill();
        Color c = _glow.color;
        c.a = 0f;
        _glow.color = c;
    }

    void FadeGlow(float targetAlpha, float duration)
    {
        if (_glow == null) return;
        _glowTween?.Kill();
        _glowTween = _glow.DOFade(targetAlpha, duration)
            .SetEase(_controller.GlowFadeEase)
            .SetUpdate(true);
    }
}
