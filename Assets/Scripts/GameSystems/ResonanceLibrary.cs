using UnityEngine;

public class ResonanceLibrary : MonoBehaviour
{
    public static ResonanceLibrary Instance;
    public ResonanceLibraryObject resonanceLibrary;

    void Awake()
    {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
            return;
        }
    }
    public Resonance GetResonance(ResonanceType type)
    {
        return resonanceLibrary.allResonances.Find(r => r.ResonanceType == type);
    }
}