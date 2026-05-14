using System.Threading.Tasks;
using System.Collections.Generic;
using GameSystems;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;


public interface IEventTrigger //TODO combine with handle event maybe?
{
    Task Execute(EffectContext context);
    bool CanTrigger(GameEvent gameEvent, TriggerBinding binding);
}

//GAME FLOW LOGIC
[System.Serializable]
public class OnRoundStart : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext instance)
    {
        if (effect == null)
        {
            Debug.LogError("Invalid effect assigned to OnRoundStartEffect, skipping execution.");
            return;
        }

        Debug.Log("Executing start of round logic");
        await effect.Execute(instance);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnRoundStart;
}

[System.Serializable]
public class OnRoundEnd : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext instance)
    {
        if (effect == null)
        {
            Debug.LogError("Invalid effect assigned to OnRoundEndEffect, skipping execution.");
            return;
        }

        Debug.Log("Executing end of round logic");
        await effect.Execute(instance);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnRoundEnd;
}

[System.Serializable]
public class OnPlayed : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("Invalid effect assigned to OnPlayed, skipping execution.");
            return;
        }

        Debug.Log("Executing on played logic");
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnPlayed;
}

[System.Serializable]
public class OnCombatResolution : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect = new DefaultAttackEffect();

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("Invalid effect assigned to OnCombatResolution, skipping execution.");
            return;
        }

        Debug.Log("Executing on combat resolution logic");
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnCombatResolution;
}

[System.Serializable]
public class OnAboutToTakeDamage : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        Debug.Log("Executing on about to take damage logic");
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnAboutToTakeDamage;
}

[System.Serializable]
public class OnDamageRecieved : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        Debug.Log("Executing on damaged logic");
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnDamaged;
}

[System.Serializable]
public class OnEveryNthRound : IEventTrigger
{
    public int roundInterval;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (context.EffectContextPayload is GameEvent gameEvent)
        {
            if (gameEvent.GameEventPayload is int currentRound)
            {
                if (context.Instance is FieldableCardInstance fieldableCardInstance)
                {
                    if ((currentRound - fieldableCardInstance.SummonedOnRound) % roundInterval == 0)
                    {
                        Debug.Log($"Executing every {roundInterval} rounds logic on round {currentRound}");
                        await effect.Execute(context);
                    }
                }
                else
                {
                    Debug.LogError(
                        "OnEveryNthRound effect requires the instance to be a FieldableCardInstance, skipping execution.");
                }
            }
        }
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnRoundStart;
}

[System.Serializable]
public class AfterNRoundsPassedDoOnce : IEventTrigger
{
    public int roundsToWait;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
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
                        await effect.Execute(context);
                    }
                }
            }
        }
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnRoundStart;
}

[System.Serializable]
public class OnDrawCard : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ITargetLogic targetThatDrewCard;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        foreach (var target in targetThatDrewCard.GetTargets(context))
        {
            if (context.EffectContextPayload is GameEvent gameEvent &&
                gameEvent.GameEventPayload is ITargetable cardDrawer)
            {
                if (target == cardDrawer)
                {
                    Debug.Log($"Card drawn by target {target}, executing on draw card logic.");
                    await effect.Execute(context);
                }
            }
        }
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        return gameEvent.Type == GameEventType.OnCardDrawn;
    }
}

[System.Serializable]
public class OnDiscardCard : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ITargetLogic targetThatDiscardedCard;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        foreach (var target in targetThatDiscardedCard.GetTargets(context))
        {
            if (context.EffectContextPayload is GameEvent gameEvent &&
                gameEvent.GameEventPayload is ITargetable cardDiscarder)
            {
                if (target == cardDiscarder)
                {
                    Debug.Log($"Card discarded by target {target}, executing on discard card logic.");
                    await effect.Execute(context);
                }
            }
        }
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        return gameEvent.Type == GameEventType.OnCardDiscarded;
    }
}

[System.Serializable]
public class OnAboutToAttack : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("Invalid effect assigned to OnAttack, skipping execution.");
            return;
        }

        Debug.Log("Executing on attack logic");
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnAboutToAttack;
}

[System.Serializable]
public class OnAttack : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("Invalid effect assigned to OnAttack, skipping execution.");
            return;
        }

        Debug.Log("Executing on attack logic");
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnAttack;
}

[System.Serializable]
public class OnEffectFieldIsActivated : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("Invalid effect assigned to OnEffectFieldIsActivated, skipping execution.");
            return;
        }
        Debug.Log("Executing on effect field is activated logic");
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    { 
        return gameEvent.Type == GameEventType.OnActivateEffectEvent
               && gameEvent.GameEventPayload is EffectFieldPosition fieldPosition
               && fieldPosition == binding.EffectIndex;
    }
}
[System.Serializable]
public class OnEffectFieldIsDeActivated : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("Invalid effect assigned to OnEffectFieldIsDeActivated, skipping execution.");
            return;
        }
        Debug.Log("Executing on effect field is deactivated logic");
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        return gameEvent.Type == GameEventType.OnDeactivateEffectEvent
               && gameEvent.GameEventPayload is EffectFieldPosition fieldPosition
               && fieldPosition == binding.EffectIndex;
    }
}
[System.Serializable]
public class OnStatusEffectApplied : IEventTrigger
{
    public StatusEffectMask filter;
    
    [SerializeReference] [SubclassSelector]
    private ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect != null)
        {
            await effect.Execute(context);
        }
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        // 1. Check if the event type matches
        if (gameEvent.Type != GameEventType.OnStatusEffectApplied) return false;

        // 2. Extract the payload
        if (gameEvent.GameEventPayload is StatusEffectInstance statusEffectInstance)
        {
            StatusEffectType appliedType = statusEffectInstance.Data.effectName;
            
            // 3. BITWISE CHECK
            // We shift '1' by the index of the enum to create a mask, then AND it with our filter.
            // Example: If Burn is index 2, (1 << 2) creates 0100.
            int mask = 1 << ((int)appliedType); 
            
            return ( (int)filter & mask ) != 0 || filter == StatusEffectMask.All;
        }

        return false;
    }
}
[System.Serializable]
public class OnStatusEffectRemoved : IEventTrigger
{
    public StatusEffectMask filter;
    
    [SerializeReference] [SubclassSelector]
    private ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect != null)
        {
            await effect.Execute(context);
        }
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        if (gameEvent.Type != GameEventType.OnStatusEffectRemoved) return false;

     
        if (gameEvent.GameEventPayload is StatusEffectInstance statusEffectInstance)
        {
            StatusEffectType appliedType = statusEffectInstance.Data.effectName;
            int mask = 1 << ((int)appliedType); 
            
            return ( (int)filter & mask ) != 0 || filter == StatusEffectMask.All;
        }

        return false;
    }
}
[System.Serializable]
public class OnKilled : IEventTrigger
{
    [SerializeReference]
    [SubclassSelector]
    ICardEffect effect;

    public Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("Invalid effect assigned to OnKilled, skipping execution.");
            return Task.CompletedTask;
        }

        Debug.Log("Executing on killed logic");
        return effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        return gameEvent.Type == GameEventType.OnKilled;
    }
}
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
