using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResonanceLibrary", menuName = "Cards/ResonanceLibrary")]
public class ResonanceLibrary: ScriptableObject
{
    private static ResonanceLibrary _instance;

    public static ResonanceLibrary Instance
    {
        get
        {
            if (_instance == null)
            {
                // Loads the asset from Assets/Resources/ResonanceLibrary.asset
                _instance = Resources.Load<ResonanceLibrary>("ResonanceLibrary");

                if (_instance == null)
                {
                    Debug.LogError("ResonanceLibrary asset not found! Make sure it is in a 'Resources' folder and named 'ResonanceLibrary'.");
                }
            }
            return _instance;
        }
    }

    public List<Resonance> allResonances;

    public Resonance GetResonance(ResonanceType type)
    {
        return allResonances.Find(r => r.ResonanceType == type);
    }
}