using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put this on the same GameObject as the icon's Image component. Call
/// SetIcon(sprite) whenever you assign a rune/gem/artwork to a slot - the drop
/// shadow (and optional contact AO / grade) is entirely shader-driven off the
/// sprite's own alpha, so any icon "just works" with no extra art.
///
/// This intentionally does NOT set the sprite the way a plain visualizer does
/// (image.sprite = x). For the offset shadow to render, the Image has to draw
/// its full quad (Type = Simple, Use Sprite Mesh = OFF), which this component
/// forces. The remaining requirement is on the sprite asset import settings:
/// Mesh Type = Full Rect, and transparent padding around the icon in the texture
/// so the offset + blur stay in-bounds.
/// </summary>
[RequireComponent(typeof(Image))]
public class IconSocket : MonoBehaviour
{
    [SerializeField] private Shader iconSocketShader; // assign "UI/IconSocketURP" in the inspector

    [Header("Drop shadow")]
    public Vector2 shadowOffset = new Vector2(6, -8);
    [Range(0, 16)] public float shadowBlur = 5f;
    [Range(0, 2)] public float shadowStrength = 1.0f;
    [Range(0.2f, 3f)] public float shadowFalloff = 0.7f; // lower = denser/more intense
    public Color shadowColor = new Color(0f, 0f, 0f, 0.8f); // alpha scales the shadow

    [Header("Contact AO (0 = off)")]
    [Range(0, 16)] public float aoBlur = 6f;
    [Range(0, 1)] public float aoStrength = 0f;

    [Header("Icon grade (identity = untouched icon)")]
    [Range(0, 2)] public float saturation = 1.0f;
    [Range(0, 2)] public float brightness = 1.0f;
    public Color ambientTint = new Color(0.35f, 0.4f, 0.45f);
    [Range(0, 1)] public float ambientTintAmount = 0f;

    private Image _image;
    private Material _materialInstance;

    private void Awake()
    {
        _image = GetComponent<Image>();

        // Instance the material once per icon slot so tuning one card doesn't
        // affect every other card sharing the same base shader.
        var shader = iconSocketShader != null ? iconSocketShader : Shader.Find("UI/IconSocketURP");
        _materialInstance = new Material(shader);
        _image.material = _materialInstance;

        ConfigureImage();
        ApplyProperties();
    }

    /// <summary>
    /// Force the Image into the mode the shadow needs: draw the full sprite quad,
    /// not a tight mesh clipped to the opaque pixels (which would cut the shadow off).
    /// </summary>
    private void ConfigureImage()
    {
        if (_image == null) _image = GetComponent<Image>();
        _image.type = Image.Type.Simple;
        _image.useSpriteMesh = false; // full-rect quad -> room around the icon for the shadow
        _image.preserveAspect = true;
    }

    /// <summary>Call this whenever a different icon needs to appear in this slot.</summary>
    public void SetIcon(Sprite icon)
    {
        if (_image == null) _image = GetComponent<Image>();
        _image.sprite = icon;
        ConfigureImage();
        // No further setup needed - the shader reads the new sprite's alpha for the
        // shadow/AO shape automatically on the next frame.
    }

    /// <summary>Push the inspector-tuned values into the material (call after changing them at runtime).</summary>
    public void ApplyProperties()
    {
        if (_materialInstance == null) return;

        _materialInstance.SetVector("_ShadowOffset", shadowOffset);
        _materialInstance.SetFloat("_ShadowBlur", shadowBlur);
        _materialInstance.SetFloat("_ShadowStrength", shadowStrength);
        _materialInstance.SetFloat("_ShadowFalloff", shadowFalloff);
        _materialInstance.SetColor("_ShadowColor", shadowColor);

        _materialInstance.SetFloat("_AOBlur", aoBlur);
        _materialInstance.SetFloat("_AOStrength", aoStrength);

        _materialInstance.SetFloat("_Saturation", saturation);
        _materialInstance.SetFloat("_Brightness", brightness);
        _materialInstance.SetColor("_AmbientTint", ambientTint);
        _materialInstance.SetFloat("_AmbientTintAmount", ambientTintAmount);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_materialInstance != null) ApplyProperties();
    }
#endif
}
