using System.Collections.Generic;
using GameSystems;
using NUnit.Framework;
using UnityEngine;

public interface ICardEffect
{
    void Execute(CardContext context);
}
public interface ITriggeredEffect : ICardEffect
{
    bool CanTrigger(GameEventType eventType);
}

//GAME FLOW LOGIC
[System.Serializable]
public class OnRoundStartEffect : ITriggeredEffect
{
    [SerializeReference]
    [SubclassSelector]
    ICardEffect effect;
    public void Execute(CardContext context)
    {
        if (effect == null || effect is OnRoundStartEffect)
        {
            Debug.LogError("Invalid effect assigned to OnRoundStartEffect, skipping execution.");
            return;
        }
       
        Debug.Log("Executing start of round logic");
        effect.Execute(context);
    }

    public bool CanTrigger(GameEventType eventType) => eventType == GameEventType.OnRoundStart;
}

[System.Serializable]
public class OnRoundEndEffect : ITriggeredEffect
{
    [SerializeReference]
    [SubclassSelector]
    ICardEffect effect;
    public void Execute(CardContext context)
    {
        if (effect == null || effect is OnRoundEndEffect)
        {
            Debug.LogError("Invalid effect assigned to OnRoundEndEffect, skipping execution.");
            return;
        }
       
        Debug.Log("Executing end of round logic");
        effect.Execute(context);
    }
    public bool CanTrigger(GameEventType eventType) => eventType == GameEventType.OnRoundEnd;
}

[System.Serializable]
public class OnPlayed : ITriggeredEffect
{
    [SerializeReference]
    [SubclassSelector]
    ICardEffect effect;
    public void Execute(CardContext context)
    {
        if (effect == null || effect is OnPlayed)
        {
            Debug.LogError("Invalid effect assigned to OnPlayed, skipping execution.");
            return;
        }
        Debug.Log("Executing on played logic");
        effect.Execute(context);
    }
    public bool CanTrigger(GameEventType eventType) => eventType == GameEventType.OnPlayed;
}

//COMBAT LOGIC
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
public class CustomLogicEffect : ICardEffect //Escape hatch for  complex logic without the lego bricks
{
    public void Execute(CardContext context)
    {
        Debug.Log("Executing hyper-complex chaos logic");
    }
}

//
// [System.Serializable]
// public class DrawEffect : ICardEffect
// {
//     public int count;
//
//     public void Execute(CardContext context) 
//     {
//         Debug.Log($"Drawing {count} cards");
//         Debug.LogWarning("NOT IMPLEMENTED");
//     }
// }
// [System.Serializable]
// public class DiscardEffect : ICardEffect
// {
//     public int count;
//     bool randomDiscard; //If true, discard random cards. If false, discard from the end of the hand.
//
//     public void Execute(CardContext context) 
//     {
//         Debug.Log($"Discarding {count} cards {(randomDiscard ? "randomly" : "chosen by player")}");
//         Debug.LogWarning("NOT IMPLEMENTED");
//     }
// }

//
// [System.Serializable]
// public class OnDamageRecieved : ICardEffect
// {
//     [SerializeReference] [SubclassSelector]
//     ICardEffect effect;
//
//     public void Execute(CardContext context)
//     {
//         if (effect == null || effect is OnDamageRecieved)
//         {
//             Debug.LogError("Invalid effect assigned to OnDamaged, skipping execution.");
//             return;
//         }
//
//         Debug.Log("Executing on damaged logic");
//         effect.Execute(context);
//     }
// }
//
// [System.Serializable]
// public class OnAttack : ICardEffect
// {
//     [SerializeReference] [SubclassSelector]
//     ICardEffect effect;
//
//     public void Execute(CardContext context)
//     {
//         if (effect == null || effect is OnAttack)
//         {
//             Debug.LogError("Invalid effect assigned to OnAttack, skipping execution.");
//             return;
//         }
//
//         Debug.Log("Executing on attack logic");
//         Debug.LogWarning("NOT WIRED UP TO ACTUALL TRIGGER YET");
//         effect.Execute(context);
//     }
// }
// [System.Serializable]
// public class OnKilled : ICardEffect
// {
//     [SerializeReference]
//     [SubclassSelector]
//     ICardEffect effect;
//     public void Execute(CardContext context)
//     {
//         if (effect == null || effect is OnKilled)
//         {
//             Debug.LogError("Invalid effect assigned to OnKilled, skipping execution.");
//             return;
//         }
//         Debug.Log("Executing on killed logic");
//         Debug.LogWarning("NOT WIRED UP TO ACTUALL TRIGGER YET");
//         effect.Execute(context);
//     }
// }
// [System.Serializable]
// public class OnChance : ICardEffect
// {
//     //TODO figue out where this gets triggered since its gonna be like a on round start or on damage recieved
//     [UnityEngine.Range(0,1)]
//     public float chance;
//     [SerializeReference]
//     [SubclassSelector]
//         ICardEffect effect;
//         public void Execute(CardContext context)
//         {
//             if (effect == null || effect is OnChance)
//             {
//                 Debug.LogError("Invalid effect assigned to OnChance, skipping execution.");
//                 return;
//             }
//             if (Random.value <= chance)
//             {
//                 Debug.Log($"Chance succeeded ({chance * 100}%), executing logic.");
//                 Debug.LogWarning("NOT WIRED UP TO ACTUALL TRIGGER YET");
//                 effect.Execute(context);
//             }
//             else
//             {
//                 Debug.Log($"Chance failed ({chance * 100}%), skipping logic.");
//             }
//         }
//    
// }
// [System.Serializable]
// public class OnStatusEffect : ICardEffect
// {
//     public void Execute(CardContext context)
//     {
//         Debug.Log("Executing on damaged logic");
//     }
// }


