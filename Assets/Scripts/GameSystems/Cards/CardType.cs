using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;

[Serializable]
public abstract class CardType
{
}

[Serializable]
public class FieldableCardType : CardType
{
    [Header("Keywords")]
    public List<KeywordData> keywords = new();

    [Header("Logic")]
    
    [SerializeReference]
    public Rune[] effectActivatingRunes = new Rune[2]; //the runes that need to be matched to activate the effects. 
   
    [SerializeReference]
    [SubclassSelector]
    public List<IEventTrigger> PassiveEventTriggers = new(); //TODO continue here by making this event or conditional possibly both deriving from same baseclass
    [SerializeReference]
    [SubclassSelector]
    public List<IEventTrigger> Effect1EventTriggers = new();
    [SerializeReference]
    [SubclassSelector]
    public List<IEventTrigger> Effect2EventTriggers = new();

    public String passiveDescription;
    public String effect1Description;
    public String effect2Description;
}
[Serializable]
public class MinionType : FieldableCardType
{
    [SerializeReference]
    [SubclassSelector]
    public IEventTrigger DefaultCombatBehaviour = new OnGameEvent { type = GameEventType.OnCombatResolution };
    public int baseHealth;
    public int baseAttack;
}

// [Serializable]
// public class HeroType : CardData 
// {
//     public int health;
//     public String heroName;
// }

[Serializable]
public class ItemType : FieldableCardType
{
    [SerializeReference] //only items and spells have activator runes, minions and traps don't have activator runes.
    public Rune[] suppliedActivatorRunes = new Rune[2]; //Runes that activate the neighboring cards' effects. if the runes match the neighboring card's EffectActivatingRunes, then the neighboring card's effects are active.
}
[Serializable]
public class SpellType : CardType
{
    [SerializeReference]
    [SubclassSelector]
    public List<IEventTrigger> SpellEffects = new();
    public String SpellDescription;
}

[Serializable]
public class TrapType : CardType
{
    public bool hidden;
}