using System.Threading.Tasks;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;


public interface IEventTrigger
{
    Task Execute(EffectContext context);
    bool CanTrigger(GameEvent gameEvent, TriggerBinding binding);
}

// Replaces: OnRoundStart, OnRoundEnd, OnPlayed, OnCombatResolution,
//           OnAboutToTakeDamage, OnDamageRecieved, OnAboutToAttack, OnAttack, OnKilled
[System.Serializable]
public class OnGameEvent : IEventTrigger
{
    public GameEventType type;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError($"No effect assigned to OnGameEvent ({type}), skipping execution.");
            return;
        }
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == type;
}

// KEPT: checks (currentRound - summonedOnRound) % roundInterval == 0
[System.Serializable]
public class OnEveryNthRound : IEventTrigger
{
    public int roundInterval;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (context.EffectContextPayload is GameEvent gameEvent && gameEvent.GameEventPayload is int currentRound)
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
                Debug.LogError("OnEveryNthRound requires a FieldableCardInstance, skipping execution.");
            }
        }
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnRoundStart;
}

// KEPT: fires exactly once when (currentRound - summonedOnRound) == roundsToWait
[System.Serializable]
public class AfterNRoundsPassedDoOnce : IEventTrigger
{
    public int roundsToWait;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (context.EffectContextPayload is GameEvent gameEvent && gameEvent.GameEventPayload is int currentRound)
        {
            if (context.Instance is FieldableCardInstance fieldableCardInstance)
            {
                if ((currentRound - fieldableCardInstance.SummonedOnRound) == roundsToWait)
                {
                    Debug.Log($"Executing delayed logic after {roundsToWait} rounds, on round {currentRound}");
                    await effect.Execute(context);
                }
            }
        }
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnRoundStart;
}

// KEPT: filters by which specific player drew the card via ITargetLogic
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

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnCardDrawn;
}

// KEPT: filters by which specific player discarded via ITargetLogic
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

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnCardDiscarded;
}

// KEPT: payload field position must match binding.EffectIndex — requires binding context
[System.Serializable]
public class OnEffectFieldIsActivated : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("No effect assigned to OnEffectFieldIsActivated, skipping execution.");
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

// KEPT: payload field position must match binding.EffectIndex — requires binding context
[System.Serializable]
public class OnEffectFieldIsDeActivated : IEventTrigger
{
    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("No effect assigned to OnEffectFieldIsDeActivated, skipping execution.");
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

// KEPT: bitwise mask filter — which specific status effect type was applied
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
        if (gameEvent.Type != GameEventType.OnStatusEffectApplied) return false;

        if (gameEvent.GameEventPayload is StatusEffectInstance statusEffectInstance)
        {
            StatusEffectType appliedType = statusEffectInstance.Data.effectName;
            int mask = 1 << (int)appliedType;
            return ((int)filter & mask) != 0 || filter == StatusEffectMask.All;
        }

        return false;
    }
}

// KEPT: bitwise mask filter — which specific status effect type was removed
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
            int mask = 1 << (int)appliedType;
            return ((int)filter & mask) != 0 || filter == StatusEffectMask.All;
        }

        return false;
    }
}
