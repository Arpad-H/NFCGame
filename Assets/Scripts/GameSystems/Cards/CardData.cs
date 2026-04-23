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

    [Header("Keywords")]
    public List<KeywordData> keywords = new List<KeywordData>();

    [Header("Logic")]
    [SerializeReference] 
    public List<ICardEffect> effects = new List<ICardEffect>();
}

[Serializable]
public abstract class CardType
{
    
}
[Serializable]
public class MinionType : CardType 
{
    public int health;
    public int attack;
}


public class SpellType : CardData 
{
    //spell-specific properties can go here
}
public class TrapType : CardType
{
    public bool hidden;
}