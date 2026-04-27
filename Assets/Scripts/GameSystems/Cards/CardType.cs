using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class CardType
{
}

[Serializable]
public class MinionType : CardType
{
    public int baseHealth;
    public int baseAttack;

    [Header("Keywords")]
    public List<KeywordData> keywords = new();

    [Header("Logic")]
    [SerializeReference]
    [SubclassSelector]
    public List<IEventTrigger> effects = new();
}

// [Serializable]
// public class HeroType : CardData 
// {
//     public int health;
//     public String heroName;
// }

[Serializable]
public class SpellType : CardData
{
    //spell-specific properties can go here
}

[Serializable]
public class TrapType : CardType
{
    public bool hidden;
}