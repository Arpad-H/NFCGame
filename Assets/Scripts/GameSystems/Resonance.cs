using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Resonance", menuName = "Cards/Resonance")]
public class Resonance : ScriptableObject
{
    public ResonanceType ResonanceType;
    public string name;
    public string identity;
    public Color color;
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
[CreateAssetMenu(menuName = "Cards/Resonance Library Object")]
public class ResonanceLibraryObject : ScriptableObject
{
    // Right-click the word "All Resonances" in the Inspector to see the option
    [ContextMenuItem("Update List", "RefreshLibrary")]
    public List<Resonance> allResonances;
   
}
