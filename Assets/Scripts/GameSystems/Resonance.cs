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

    [Header("Floor decal (projected rune in front of the portal)")]
    [Tooltip("Silhouette/mask of this resonance's rune. Written into the decal " +
             "projector's _MaskTex so each portal shows its own symbol.")]
    public Texture2D decalMask;

    [Tooltip("Engraved-relief normal map for this resonance's rune. Written into " +
             "the decal projector's _NormalMap.")]
    public Texture2D decalNormal;
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

