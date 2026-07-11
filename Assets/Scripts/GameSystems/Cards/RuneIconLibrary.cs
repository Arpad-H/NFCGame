using GameSystems;
using UnityEngine;

[CreateAssetMenu(menuName = "Cards/Rune Icon Library")]
public class RuneIconLibrary : ScriptableObject
{
    [System.Serializable]
    public struct RuneEntry
    {
        public Rune rune;

        [Header("Full icon — centered effect/passive column (never flips)")]
        public Sprite iconInactive;
        public Sprite iconGlowing;

        [Header("Left half (v1) — rune slot on the card's LEFT edge")]
        public Sprite iconInactiveLeft;
        public Sprite iconGlowingLeft;

        [Header("Right half (v2) — rune slot on the card's RIGHT edge")]
        public Sprite iconInactiveRight;
        public Sprite iconGlowingRight;
    }

    public RuneEntry[] entries;

    // ---- Full icons -----------------------------------------------------
    // Used by the centered effect/passive icons, which sit in the text column
    // and never move to an edge, so they keep the whole-hexagon artwork.

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

    // ---- Half icons -----------------------------------------------------
    // The edge rune slots and the item stat-slot runes are drawn as a half
    // hexagon that hugs the card border, so they pick the half that matches the
    // side they sit on: Left = v1, Right = v2.

    public Sprite GetIcon(Rune rune, PlayerSide side)
    {
        foreach (var e in entries)
            if (e.rune == rune)
                return side == PlayerSide.Right ? e.iconInactiveRight : e.iconInactiveLeft;
        return null;
    }

    public Sprite GetGlowIcon(Rune rune, PlayerSide side)
    {
        foreach (var e in entries)
            if (e.rune == rune)
                return side == PlayerSide.Right ? e.iconGlowingRight : e.iconGlowingLeft;
        return null;
    }
}
