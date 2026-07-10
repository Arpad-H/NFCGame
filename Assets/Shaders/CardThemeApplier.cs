using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drop on the card's base Image (the one using UI/CardRecolor). Assign a CardTheme
/// and it pushes the colors into a per-card material clone at runtime.
///
/// A CanvasRenderer ignores MaterialPropertyBlocks, so -- unlike a MeshRenderer --
/// the only way to vary colors per card is a unique material. We clone the shared
/// material once (like CardBaseNormalLight) and write to the clone.
///
/// Deliberately NOT [ExecuteAlways]: assigning a runtime material clone to Image's
/// serialized material field in edit mode cannot be serialized, so Unity blanks the
/// slot on the next reload -- the material appears to "vanish" when you press Play.
/// In edit mode the card just renders the shared CardRecolor material's own colors,
/// which is a fine preview; the per-card theme is applied when the game runs.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class CardThemeApplier : MonoBehaviour
{
    static readonly int StoneShadow    = Shader.PropertyToID("_StoneShadow");
    static readonly int StoneHighlight = Shader.PropertyToID("_StoneHighlight");
    static readonly int BoxShadow      = Shader.PropertyToID("_BoxShadow");
    static readonly int BoxHighlight   = Shader.PropertyToID("_BoxHighlight");
    static readonly int GoldShadow     = Shader.PropertyToID("_GoldShadow");
    static readonly int GoldMid        = Shader.PropertyToID("_GoldMid");
    static readonly int GoldHighlight  = Shader.PropertyToID("_GoldHighlight");
    static readonly int RimShadow      = Shader.PropertyToID("_RimShadow");
    static readonly int RimHighlight   = Shader.PropertyToID("_RimHighlight");
    static readonly int CrackEmissive  = Shader.PropertyToID("_CrackEmissive");
    static readonly int NoiseColor     = Shader.PropertyToID("_NoiseColor");
    static readonly int Tint           = Shader.PropertyToID("_Tint");

    public CardTheme theme;
    [Range(0f, 1f)] public float fade = 1f;

    Graphic _graphic;
    Material _instance;   // per-card runtime clone; never the shared asset

    void Awake() => EnsureInstance();

    void OnEnable() => Apply();

    void OnDestroy()
    {
        if (_instance == null) return;
        if (Application.isPlaying) Destroy(_instance);
        else DestroyImmediate(_instance);
    }

    // Graphic.material hands back the shared asset; clone once so each card can carry
    // its own colors without writing through to the project material. Lazy so it's
    // safe to call from CardVisualizer.Setup before Awake runs (e.g. a freshly
    // instantiated, still-inactive card).
    void EnsureInstance()
    {
        if (_instance != null) return;
        if (_graphic == null) _graphic = GetComponent<Graphic>();
        var shared = _graphic.material;
        if (shared == null || shared.shader == null) return;
        _instance = new Material(shared) { name = shared.name + " (card instance)" };
        _graphic.material = _instance;
    }

    /// <summary>Swap to a theme resolved elsewhere (e.g. from the card's resonance) and apply.</summary>
    public void SetTheme(CardTheme newTheme)
    {
        theme = newTheme;
        Apply();
    }

    /// <summary>Push the theme's colors onto this card's material. Safe to call at runtime.</summary>
    public void Apply()
    {
        EnsureInstance();
        if (_instance == null || theme == null) return;

        _instance.SetColor(StoneShadow,    theme.stoneShadow);
        _instance.SetColor(StoneHighlight, theme.stoneHighlight);
        _instance.SetColor(BoxShadow,      theme.boxShadow);
        _instance.SetColor(BoxHighlight,   theme.boxHighlight);
        _instance.SetColor(GoldShadow,     theme.goldShadow);
        _instance.SetColor(GoldMid,        theme.goldMid);
        _instance.SetColor(GoldHighlight,  theme.goldHighlight);
        _instance.SetColor(RimShadow,      theme.rimShadow);
        _instance.SetColor(RimHighlight,   theme.rimHighlight);
        // HDR: intensity above 1 is what makes bloom kick in.
        _instance.SetColor(CrackEmissive,  theme.crackEmissive * theme.crackIntensity);
        _instance.SetColor(NoiseColor,     theme.noiseColor * theme.noiseIntensity);
        _instance.SetColor(Tint,           new Color(1f, 1f, 1f, fade));
    }
}

[CreateAssetMenu(menuName = "Cards/Card Theme", fileName = "CardTheme")]
public class CardTheme : ScriptableObject
{
    [Header("Stone Background")]
    public Color stoneShadow    = new Color(0.051f, 0.078f, 0.141f);
    public Color stoneHighlight = new Color(0.427f, 0.518f, 0.659f);

    [Header("Text Box")]
    public Color boxShadow    = new Color(0.180f, 0.180f, 0.188f);
    public Color boxHighlight = new Color(0.608f, 0.608f, 0.616f);

    [Header("Gold (3-stop metal ramp)")]
    public Color goldShadow    = new Color(0.078f, 0.051f, 0.027f);
    public Color goldMid       = new Color(0.431f, 0.282f, 0.149f);
    public Color goldHighlight = new Color(0.769f, 0.592f, 0.412f);

    [Header("Rim")]
    public Color rimShadow    = new Color(0.031f, 0.039f, 0.051f);
    public Color rimHighlight = new Color(0.494f, 0.451f, 0.420f);

    [Header("Crack Glow")]
    [ColorUsage(false, true)] public Color crackEmissive = Color.black;
    [Min(0f)] public float crackIntensity = 1f;

    [Header("Rolling Noise")]
    [ColorUsage(false, true)] public Color noiseColor = Color.black;
    [Min(0f)] public float noiseIntensity = 1f;
}
