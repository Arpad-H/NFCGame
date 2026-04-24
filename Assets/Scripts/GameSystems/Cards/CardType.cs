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
    public List<ICardEffect> effects = new();
}

public sealed class MinionInstance : ITargetable
{
    public CardData SourceCard { get; }
    public MinionType Definition { get; }

    public int CurrentHealth { get; private set; }
    public int CurrentAttack { get; private set; }

    public event Action<int> OnHealthChanged;
    public event Action OnDeath;

    public MinionInstance(CardData sourceCard, MinionType definition)
    {
        SourceCard = sourceCard;
        Definition = definition;
        CurrentHealth = definition.baseHealth;
        CurrentAttack = definition.baseAttack;
    }

    public void TakeDamage(int amount)
    {
        CurrentHealth -= amount;
        OnHealthChanged?.Invoke(CurrentHealth);
        if (CurrentHealth <= 0)
        {
          
            OnDeath?.Invoke();
        }
    }

    public void ResolveEffects(CardContext context)
    {
        foreach (var effect in Definition.effects)
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