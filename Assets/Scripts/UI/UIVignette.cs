using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A tweakable full-screen vignette that draws as UGUI, so it darkens everything on its canvas —
/// including Screen Space - Overlay UI that URP's post-process Vignette can't touch. Drop the
/// prefab (see <c>GameObject ▸ UI ▸ Vignette</c>) into any scene: the main menu, the game scene,
/// anywhere. It's pipeline-independent and needs no scene Volume or camera setup.
///
/// It's a <see cref="MaskableGraphic"/>, so it renders a single full-rect quad using the
/// <c>UI/Vignette</c> shader and owns a private material instance — every field below is live in
/// the inspector (edit and play mode) and settable from script for fades / juice. Put it on a
/// full-screen stretched RectTransform, ideally on its own high sort-order Overlay canvas so it
/// sits above the rest of the UI. Raycasts are off by default so it never eats clicks.
/// </summary>
[AddComponentMenu("UI/Effects/UI Vignette")]
[DisallowMultipleComponent]
public class UIVignette : MaskableGraphic
{
    [Tooltip("The UI/Vignette shader. Assign the asset so it ships in builds; otherwise it's " +
             "looked up by name (reliable in the editor and if the shader is Always Included).")]
    [SerializeField] Shader vignetteShader;

    // Tint comes from the inherited Graphic.color field (shown as "Color" in the inspector); its
    // alpha scales overall strength. Black is the usual choice.

    [Tooltip("0 = no darkening at the edges (off), 1 = the darkening reaches the centre.")]
    [Range(0f, 1f)] [SerializeField] float intensity = 0.4f;

    [Tooltip("Width of the fade from clear to fully tinted. Low = a hard ring, high = a soft wash.")]
    [Range(0.001f, 1f)] [SerializeField] float smoothness = 0.5f;

    [Tooltip("0 = ellipse that follows the screen aspect (corners darken evenly), 1 = a true circle.")]
    [Range(0f, 1f)] [SerializeField] float roundness = 0f;

    [Tooltip("Where the clear centre sits, in 0-1 rect space. (0.5, 0.5) is the middle.")]
    [SerializeField] Vector2 center = new Vector2(0.5f, 0.5f);

    static readonly int IntensityID = Shader.PropertyToID("_Intensity");
    static readonly int SmoothnessID = Shader.PropertyToID("_Smoothness");
    static readonly int RoundedID = Shader.PropertyToID("_Rounded");
    static readonly int CenterXID = Shader.PropertyToID("_CenterX");
    static readonly int CenterYID = Shader.PropertyToID("_CenterY");

    Material _material;

    // ---- Runtime-tweakable API (drive these to fade the vignette in/out or pulse it) ----
    // For the tint, set the inherited Graphic.color directly (its alpha scales strength).

    /// <summary>0 = off (edges only), 1 = darkening reaches the centre.</summary>
    public float Intensity
    {
        get => intensity;
        set { intensity = Mathf.Clamp01(value); PushProperties(); }
    }

    /// <summary>Width of the clear-to-tinted fade band.</summary>
    public float Smoothness
    {
        get => smoothness;
        set { smoothness = Mathf.Clamp(value, 0.001f, 1f); PushProperties(); }
    }

    /// <summary>0 = screen-aspect ellipse, 1 = perfect circle.</summary>
    public float Roundness
    {
        get => roundness;
        set { roundness = Mathf.Clamp01(value); PushProperties(); }
    }

    /// <summary>Clear centre in 0-1 rect space.</summary>
    public Vector2 Center
    {
        get => center;
        set { center = value; PushProperties(); }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;   // an overlay must never swallow clicks
        EnsureMaterial();
        PushProperties();
    }

    // A white 1x1 keeps the shader's _MainTex sample a no-op, so the tint is pure vertex colour.
    public override Texture mainTexture => s_WhiteTexture;

    void EnsureMaterial()
    {
        if (_material != null) return;
        if (vignetteShader == null) vignetteShader = Shader.Find("UI/Vignette");
        if (vignetteShader == null) return;   // material stays null; graphic renders with default
        _material = new Material(vignetteShader) { hideFlags = HideFlags.HideAndDontSave };
        material = _material;
    }

    void PushProperties()
    {
        if (_material == null) return;
        _material.SetFloat(IntensityID, intensity);
        _material.SetFloat(SmoothnessID, smoothness);
        _material.SetFloat(RoundedID, roundness);
        _material.SetFloat(CenterXID, center.x);
        _material.SetFloat(CenterYID, center.y);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_material != null) DestroyImmediate(_material);
    }

#if UNITY_EDITOR
    protected override void Reset()
    {
        base.Reset();
        raycastTarget = false;
        color = Color.black;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureMaterial();
        PushProperties();
    }
#endif
}
