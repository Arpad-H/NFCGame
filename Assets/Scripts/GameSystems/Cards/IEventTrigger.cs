using System.Collections.Generic;
using GameSystems;
using NUnit.Framework;
using UnityEngine;


public interface IEventTrigger : ICardEffect //TODO combine with handle event maybe?
{
    bool CanTrigger(GameEventType eventType);
}

//GAME FLOW LOGIC
[System.Serializable]
public class OnRoundStartEffect : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public void Execute(EffectContext instance)
    {
        if (effect == null || effect is OnRoundStartEffect)
        {
            Debug.LogError("Invalid effect assigned to OnRoundStartEffect, skipping execution.");
            return;
        }

        Debug.Log("Executing start of round logic");
        effect.Execute(instance);
    }

    public bool CanTrigger(GameEventType eventType) => eventType == GameEventType.OnRoundStart;
}

[System.Serializable]
public class OnRoundEndEffect : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public void Execute(EffectContext instance)
    {
        if (effect == null || effect is OnRoundEndEffect)
        {
            Debug.LogError("Invalid effect assigned to OnRoundEndEffect, skipping execution.");
            return;
        }

        Debug.Log("Executing end of round logic");
        effect.Execute(instance);
    }

    public bool CanTrigger(GameEventType eventType) => eventType == GameEventType.OnRoundEnd;
}

[System.Serializable]
public class OnPlayed : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public void Execute(EffectContext context)
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

[System.Serializable]
public class OnCombatResolution : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect = new DefaultAttackEffect();

    public void Execute(EffectContext context)
    {
        if (effect == null || effect is OnCombatResolution)
        {
            Debug.LogError("Invalid effect assigned to OnCombatResolution, skipping execution.");
            return;
        }

        Debug.Log("Executing on combat resolution logic");
        effect.Execute(context);
    }

    public bool CanTrigger(GameEventType eventType) => eventType == GameEventType.OnCombatResolution;
}



[System.Serializable]
public class OnDamageRecieved : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public void Execute(EffectContext context)
    {
        Debug.Log("Executing on damaged logic");
        effect.Execute(context);
    }

    public bool CanTrigger(GameEventType eventType) => eventType == GameEventType.OnDamaged;
}

[System.Serializable]
public class OnEveryNthRound : IEventTrigger
{
    public int roundInterval;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public void Execute(EffectContext context)
    {
        if (context.EffectContextPayload is GameEvent gameEvent)
        {
            if (gameEvent.GameEventPayload is int currentRound)
            {
                if (context.Instance is FieldableCardInstance fieldableCardInstance)
                {
                    if ((currentRound - fieldableCardInstance.SummonedOnRound)%roundInterval == 0)
                    {
                        Debug.Log($"Executing every {roundInterval} rounds logic on round {currentRound}");
                        effect.Execute(context);
                    }
                }
                else
                {
                    Debug.LogError("OnEveryNthRound effect requires the instance to be a FieldableCardInstance, skipping execution.");
                }
            }
        }
    }

    public bool CanTrigger(GameEventType eventType) => eventType == GameEventType.OnRoundStart;
}
[System.Serializable]
public class AfterNRoundsPassedDoOnce : IEventTrigger
{
    public int roundsToWait;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public void Execute(EffectContext context)
    {
        if (context.EffectContextPayload is GameEvent gameEvent)
        {
            if (gameEvent.GameEventPayload is int currentRound)
            {
                if (context.Instance is FieldableCardInstance fieldableCardInstance)
                {
                    if ((currentRound - fieldableCardInstance.SummonedOnRound) == roundsToWait)
                    {
                        Debug.Log(
                            $"Executing delayed logic after {roundsToWait} rounds have passed, on round {currentRound}");
                        effect.Execute(context);
                    }
                }
            }
        }
    }

    public bool CanTrigger(GameEventType eventType) => eventType == GameEventType.OnRoundStart;
}
[System.Serializable]
public class OnDrawCard : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    ITargetLogic target;
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public void Execute(EffectContext context)
    {
        Debug.Log("Executing on draw card logic");
        effect.Execute(context);
    }
}
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