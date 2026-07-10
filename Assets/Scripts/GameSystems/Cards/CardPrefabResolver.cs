using UnityEngine;

// Single source of truth for which full-card prefab renders a given card.
// Spells use the stripped spell layout (SpellCardVisualizer); every other type
// uses the fieldable minion/item layout (FieldableCardVisualizer). Add a branch
// here when a new card type gets its own prefab.
public static class CardPrefabResolver
{
    public static GameObject Resolve(CardData card, GameObject fieldableCardPrefab, GameObject spellCardPrefab)
    {
        return card.cardType is SpellType ? spellCardPrefab : fieldableCardPrefab;
    }
}
