using System.Collections.Generic;
using UnityEngine;
using System;

public class CardLibrary //TODO THIS IS A TEMPORARY CLASS while working on a PROPER ASSET-BASED SYSTEM
{
    private static Dictionary<int, CardData> _allCards = new Dictionary<int, CardData>();

    public static void Initialize()
    {
        Sprite[] allSprites = Resources.LoadAll<Sprite>("Cards");

        foreach (Sprite s in allSprites)
        {
            // Split "Card_3_Fire_Taunt_Battlecry"
            string[] parts = s.name.Split('_');
        
            if (parts.Length >= 3 && int.TryParse(parts[1], out int id))
            {
                if (Enum.TryParse(parts[2], out ResonanceType resonance))
                {
                    CardData newCard = new CardData(parts[2] + " Spell", resonance, s);

                    // Check if there are keywords in the filename (index 3 and onwards)
                    for (int i = 3; i < parts.Length; i++)
                    {
                        string keywordName = parts[i];
                        // Load the ScriptableObject from Resources/Keywords/KeywordName
                        KeywordData kw = Resources.Load<KeywordData>($"Keywords/{keywordName}");
                    
                        if (kw != null)
                        {
                            newCard.keywords.Add(kw);
                        }
                        else
                        {
                            Debug.LogWarning($"Keyword asset '{keywordName}' not found in Resources/Keywords for card {id}");
                        }
                    }

                    _allCards[id] = newCard;
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