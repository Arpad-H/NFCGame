using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResonanceLibrary", menuName = "Cards/ResonanceLibrary")]
public class ResonanceLibrary: ScriptableObject
{
    public List<Resonance> allResonances;

    public Resonance GetResonance(ResonanceType type)
    {
        return allResonances.Find(r => r.ResonanceType == type);
    }
}