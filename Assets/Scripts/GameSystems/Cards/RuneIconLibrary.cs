using GameSystems;
using UnityEngine;

[CreateAssetMenu(menuName = "Cards/Rune Icon Library")]
public class RuneIconLibrary : ScriptableObject
{
    [System.Serializable]
    public struct RuneEntry
    {
        public Rune rune;
        public Sprite iconInactive;
        public Sprite iconGlowing;
    }

    public RuneEntry[] entries;

    public Sprite GetIcon(Rune rune)
    {
        foreach (var e in entries)
            if (e.rune == rune) return e.iconInactive;
        return null;
    }

    public Sprite GetGlowIcon(Rune rune)
    {
        foreach (var e in entries)
            if (e.rune == rune) return e.iconGlowing;
        return null;
    }
}
