using System.Collections.Generic;
using UnityEngine;
using System;

public class CardLibrary
{
    private static Dictionary<int, CardData> _allCards = new Dictionary<int, CardData>();

    public static void Initialize()
    {
        // Load all Sprites from Assets/Resources/Cards
        Sprite[] allSprites = Resources.LoadAll<Sprite>("Cards");

        foreach (Sprite s in allSprites)
        {
            // Split "Card_3_Fire" into ["Card", "3", "Fire"]
            string[] parts = s.name.Split('_');
            
            if (parts.Length == 3 && int.TryParse(parts[1], out int id))
            {
                if (Enum.TryParse(parts[2], out ResonanceType resonance))
                {
                    _allCards[id] = new CardData(parts[2] + " Spell", resonance, s);
                }
            }
        }
        Debug.Log($"Library Initialized: Loaded {_allCards.Count} cards.");
    }

    public static CardData GetCard(int id)
    {
        if (_allCards.Count == 0) Initialize(); // Lazy init
        return _allCards.ContainsKey(id) ? _allCards[id] : null;
    }
}