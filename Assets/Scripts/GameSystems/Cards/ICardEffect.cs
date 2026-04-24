using System.Collections.Generic;
using GameSystems;
using NUnit.Framework;
using UnityEngine;

public interface ICardEffect
{
    void Execute(CardContext context);
}

[System.Serializable]
public class DamageEffect : ICardEffect
{
    public int amount;
    [SerializeReference]
    [SubclassSelector]
    public ITargetLogic targetLogic; 

    public void Execute(CardContext context) 
    {
       
        var targets = targetLogic.GetTargets(context);
        foreach (var t in targets)
        {
            t.TakeDamage(amount);
            Debug.Log($"context: {context}, target: {t}, damage: {amount}");
        }
    }
}

[System.Serializable]
public class DrawEffect : ICardEffect
{
    public int count;

    public void Execute(CardContext context) 
    {
        Debug.Log($"Drawing {count} cards");
    }
}

[System.Serializable]
public class CustomLogicEffect : ICardEffect //Escape hatch for  complex logic without the lego bricks
{
    public void Execute(CardContext context)
    {
        Debug.Log("Executing hyper-complex chaos logic");
    }
}


