using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



[CreateAssetMenu(fileName = "New Card", menuName = "Cards/Card")]
public class CardData : ScriptableObject
{
    [Header("Visuals")]
    public string cardName;
    public Sprite artwork;
    public ResonanceType resonance;
    
    [Header("Type")]
    [SerializeReference]
    [SubclassSelector]
    public CardType cardType; // Could be MinionData, SpellData, etc.
}

