using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Resonance", menuName = "Cards/Resonance")]
public class Resonance : ScriptableObject
{
    public ResonanceType ResonanceType;
    public string name;
    public string identity;
    public Color color;
    public Sprite sprite;
    public GameObject portalPrefab;

    [Header("Card recolor")]
    [Tooltip("Gradient-map colors for cards of this resonance. Applied to the card " +
             "base by CardThemeApplier via CardVisualizer.")]
    public CardTheme theme;

    [Header("Floor decal (projected rune in front of the portal)")]
    [Tooltip("Silhouette/mask of this resonance's rune. Written into the decal " +
             "projector's _MaskTex so each portal shows its own symbol.")]
    public Texture2D decalMask;

    [Tooltip("Engraved-relief normal map for this resonance's rune. Written into " +
             "the decal projector's _NormalMap.")]
    public Texture2D decalNormal;

    // Lazily-built Sprite wrapping decalMask so UI (e.g. the combat-history bar)
    // can show a portal's rune as an icon. Cached at runtime so repeated lookups
    // don't re-allocate a Sprite every time a portal is hit.
    [System.NonSerialized] private Sprite _decalSprite;
    public Sprite DecalSprite
    {
        get
        {
            if (_decalSprite == null && decalMask != null)
                _decalSprite = Sprite.Create(decalMask,
                    new Rect(0f, 0f, decalMask.width, decalMask.height),
                    new Vector2(0.5f, 0.5f));
            return _decalSprite;
        }
    }
}
public enum ResonanceType
{
    Darkness,
    Plague,
    Death,
    Psychic,
    Life,
    Holy,
}

