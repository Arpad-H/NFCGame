using System.Collections.Generic;
using GameSystems;
using NUnit.Framework;
using UnityEngine;

public interface ICardEffect
{
    // You can pass a 'CardContext' object here later to handle targets/players
    void Execute(); 
}

[System.Serializable]
public class DamageEffect : ICardEffect
{
    public int amount;
    [SerializeReference]
    public ITargetLogic target; // Enum: EnemyHero, AllMinions, etc.

    public void Execute() 
    {
        Debug.Log($"Dealing {amount} damage to {target}");
        // Real logic: Find targets via a Singleton or Manager and apply damage
    }
}

[System.Serializable]
public class DrawEffect : ICardEffect
{
    public int count;

    public void Execute() 
    {
        Debug.Log($"Drawing {count} cards");
    }
}

[System.Serializable]
public class CustomLogiEffect : ICardEffect //Escape hatch for  complex logic without the lego bricks
{
    public void Execute()
    {
        Debug.Log("Executing hyper-complex chaos logic");
    }
}


