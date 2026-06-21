using GameSystems;
using UnityEngine;

[CreateAssetMenu(menuName = "Cards/Rune Icon Library")]
public class RuneIconLibrary : ScriptableObject
{
    [System.Serializable]
    public struct RuneEntry
    {
        public Rune rune;
        public Sprite icon;
    }

    public RuneEntry[] entries;

    public Sprite GetIcon(Rune rune)
    {
        foreach (var e in entries)
            if (e.rune == rune) return e.icon;
        return null;
    }
}
