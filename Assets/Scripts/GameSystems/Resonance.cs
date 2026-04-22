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
}
public enum ResonanceType
{
    Fire,
    Wind,
    Light,
    Darkness,
    Plague,
    Death,
    Spirit,
    Life,
    Gravity
}

