
using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;
using Random = System.Random;

public interface ITargetFilter
{
    List<ITargetable> Apply(List<ITargetable> input, EffectContext context);
}
[Serializable]
public class ByNameFilter : ITargetFilter
{
    public string targetName;

    public List<ITargetable> Apply(List<ITargetable> input, EffectContext context)
    {
        var result = new List<ITargetable>();
        if (string.IsNullOrWhiteSpace(targetName)) return input;

        foreach (ITargetable t in input)
        {
            if (t is MinionInstance minion && minion.SourceCard.cardName == targetName)
                result.Add(t);
        }

        return result;
    }
}
[Serializable]
public class FirstTargetFilter : ITargetFilter
{
   
    public List<ITargetable> Apply(List<ITargetable> input, EffectContext context)
    {
        var result = new List<ITargetable>();
        if (input != null && input.Count > 0) result.Add(input[0]);
        return result;
    }
}

[Serializable]
public class LastTargetFilter : ITargetFilter
{
    public List<ITargetable> Apply(List<ITargetable> input, EffectContext context)
    {
        var result = new List<ITargetable>();
        if (input != null && input.Count > 0) result.Add(input[input.Count - 1]);
        return result;
    }
}
[Serializable]
public class SelectRandom : ITargetFilter
{
    public int selectAmount;
    public List<ITargetable> Apply(List<ITargetable> input, EffectContext context)
    {
        // 1. Guard clauses for edge cases
        if (input == null || input.Count == 0 || selectAmount <= 0)
        {
            return new List<ITargetable>();
        }

        // 2. If they want more or equal to what we have, just return a copy of the whole list
        if (selectAmount >= input.Count)
        {
            return new List<ITargetable>(input);
        }

        // 3. Efficiently grab random unique elements
        // Creating a shallow copy so we don't mutate the original input list
        List<ITargetable> pool = new List<ITargetable>(input);
        List<ITargetable> result = new List<ITargetable>();
        
        // Using System.Random (If in Unity, you could use UnityEngine.Random instead)
        Random rand = new Random(); 

        for (int i = 0; i < selectAmount; i++)
        {
            int randomIndex = rand.Next(0, pool.Count);
            result.Add(pool[randomIndex]);
            
            // Remove the selected item so it can't be picked twice
            pool.RemoveAt(randomIndex);
        }

        return result;
    }
}

[Serializable]
public class HasStatusEffects : ITargetFilter
{
    public StatusEffectMask effects;
    public List<ITargetable> Apply(List<ITargetable> input, EffectContext context)
    {        
        var result = new List<ITargetable>();
        foreach (ITargetable t in input)
        {
            if (t is MinionInstance minion) {
                foreach (StatusEffectInstance sei in minion.statusEffects)
                {
                    int mask = 1 << (int)sei.Data.effectName;
                    if (((int)effects & mask) != 0)
                    {
                        result.Add(t);
                        break; //TODO currently if any of the selected match. maybe add a boolean selection later
                    }
                }
            }
             
        }

      return result;
    }
}