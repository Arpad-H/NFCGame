using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class CardType
{
}

[Serializable]
public class MinionType : CardType, ITargetable
{
    public int health;
    public int attack;
    public System.Action<int> OnHealthChanged;
    [Header("Keywords")] public List<KeywordData> keywords = new List<KeywordData>();

    [Header("Logic")] [SerializeReference] [SubclassSelector]
    public List<ICardEffect> effects = new List<ICardEffect>();

    public void TakeDamage(int amount)
    {
        health -= amount;
        OnHealthChanged?.Invoke(health);
    }
    public void ResolveEffects(CardContext context) //TODO not sure how to tricker minions attacking each other
    {
        foreach (var effect in effects)
        {
            effect.Execute(context);
        }
    }
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