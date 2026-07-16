using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// A "freeze-frame" blur backdrop for Screen Space - Overlay UI, where the interface
// isn't part of any camera texture so it can't be blurred live. Instead we grab the
// whole rendered screen once, blur that snapshot with a few Kawase passes, and show
// it on a full-screen RawImage behind whatever is being focused. It's static (the
// grid underneath isn't moving while a card is focused) and cheap — the work happens
// only at the moment of capture, not every frame.
//
// Put this on the same full-screen backdrop object as the CanvasGroup + RawImage that
// LibraryCardFocusController fades. Assign the "UI/UIScreenBlur" shader (or leave it to
// be found by name). The controller calls Capture() when a card is focused.
[RequireComponent(typeof(RawImage))]
[DisallowMultipleComponent]
public class UIScreenBlur : MonoBehaviour
{
    [Tooltip("The UI/UIScreenBlur shader. Assign the asset so it ships in builds; " +
             "otherwise it's looked up by name (editor / always-included only).")]
    [SerializeField] Shader blurShader;
    [Tooltip("How many blur passes. More = softer and wider, slightly more cost.")]
    [Range(1, 10)] [SerializeField] int iterations = 4;
    [Tooltip("Resolution divider for the blur work. 2 = quarter the pixels, softer and cheaper.")]
    [Range(1, 6)] [SerializeField] int downsample = 2;
    [Tooltip("How fast the sampling offset widens each pass. Higher = blurrier.")]
    [SerializeField] float blurSpread = 1.5f;
    [Tooltip("Tint applied to the blurred image (e.g. a slightly dark, desaturated wash).")]
    [SerializeField] Color tint = Color.white;
    [Tooltip("Flip if the captured backdrop appears upside-down on your graphics API.")]
    [SerializeField] bool flipY = true;

    RawImage _raw;
    Material _material;
    RenderTexture _result;

    void Awake()
    {
        _raw = GetComponent<RawImage>();
        if (blurShader == null) blurShader = Shader.Find("UI/UIScreenBlur");
        if (blurShader != null) _material = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
        _raw.color = tint;
    }

    // True only when the shader resolved — the controller falls back to a plain dim otherwise.
    public bool Ready => _material != null;

    // Snapshot the screen and rebuild the blurred backdrop, then invoke onReady (the
    // controller fades the panel in there, so the capture happens while it's still
    // invisible and never feeds back on itself). Returns false if it can't run, so the
    // caller can fall back to fading a plain dim immediately.
    public bool Capture(Action onReady = null)
    {
        if (_material == null || !isActiveAndEnabled) return false;
        StopAllCoroutines();
        StartCoroutine(CaptureRoutine(onReady));
        return true;
    }

    IEnumerator CaptureRoutine(Action onReady)
    {
        // Grab the fully composited frame — including all Overlay UI — after it renders.
        yield return new WaitForEndOfFrame();

        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);

        RenderTexture screen = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);

        int dw = Mathf.Max(1, w / downsample);
        int dh = Mathf.Max(1, h / downsample);
        RenderTexture a = RenderTexture.GetTemporary(dw, dh, 0, RenderTextureFormat.Default);
        RenderTexture b = RenderTexture.GetTemporary(dw, dh, 0, RenderTextureFormat.Default);

        Graphics.Blit(screen, a);
        RenderTexture.ReleaseTemporary(screen);

        for (int i = 0; i < iterations; i++)
        {
            _material.SetFloat("_Offset", 1f + i * blurSpread);
            Graphics.Blit(a, b, _material);
            (a, b) = (b, a);
        }

        // Keep a persistent copy for the RawImage; temporaries get recycled.
        if (_result != null) { _result.Release(); Destroy(_result); }
        _result = new RenderTexture(dw, dh, 0, RenderTextureFormat.Default);
        Graphics.Blit(a, _result);

        RenderTexture.ReleaseTemporary(a);
        RenderTexture.ReleaseTemporary(b);

        _raw.texture = _result;
        // CaptureScreenshotIntoRenderTexture can come back Y-flipped depending on the
        // graphics API; the toggle lets you correct it without touching the shader.
        _raw.uvRect = flipY ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);

        onReady?.Invoke();
    }

    void OnDestroy()
    {
        if (_result != null) { _result.Release(); Destroy(_result); }
        if (_material != null) Destroy(_material);
    }
}
