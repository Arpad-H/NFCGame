
using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;

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