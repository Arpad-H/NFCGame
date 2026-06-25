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

