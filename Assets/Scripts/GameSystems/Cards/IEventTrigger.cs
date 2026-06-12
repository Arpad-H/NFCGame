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
        if (context.Event.GameEventPayload is not RoundEventData roundData)
        {
            Debug.LogError($"OnEveryNthRound expected RoundEventData but got {context.Event.GameEventPayload?.GetType().Name ?? "null"}.");
            return;
        }
        if (context.Instance is not FieldableCardInstance fieldableCardInstance)
        {
            Debug.LogError("OnEveryNthRound requires a FieldableCardInstance, skipping execution.");
            return;
        }
        if ((roundData.Round - fieldableCardInstance.SummonedOnRound) % roundInterval == 0)
        {
            Debug.Log($"Executing every {roundInterval} rounds logic on round {roundData.Round}");
            await effect.Execute(context);
        }
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding) => gameEvent.Type == GameEventType.OnRoundStart;
}

// Fires exactly once, after the binding has seen roundsToWait round starts.
// Counts per card INSTANCE via the binding's StateSlot — the trigger object
// itself lives on a shared ScriptableObject and must stay stateless. No longer
// depends on SummonedOnRound or on exact round-number equality, so a missed
// round (e.g. field inactive during the matching round) can't skip it forever.
[System.Serializable]
public class AfterNRoundsPassedDoOnce : IEventTrigger
{
    public int roundsToWait;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    // Per-instance state held in TriggerBinding.StateSlot.
    private class State
    {
        public int RoundsSeen;
        public bool HasFired;
    }

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("No effect assigned to AfterNRoundsPassedDoOnce, skipping execution.");
            return;
        }

        Debug.Log($"Executing delayed logic after waiting {roundsToWait} rounds.");
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        if (gameEvent.Type != GameEventType.OnRoundStart) return false;

        if (binding.StateSlot is not State state)
        {
            binding.StateSlot = state = new State();
        }

        if (state.HasFired) return false;

        state.RoundsSeen++;
        if (state.RoundsSeen < roundsToWait) return false;

        state.HasFired = true;
        return true;
    }
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
        if (context.Event.GameEventPayload is not PlayerEventData playerData)
        {
            Debug.LogError($"OnDrawCard expected PlayerEventData but got {context.Event.GameEventPayload?.GetType().Name ?? "null"}.");
            return;
        }
        foreach (var target in targetThatDrewCard.GetTargets(context))
        {
            if (target == playerData.Player)
            {
                Debug.Log($"Card drawn by {target}, executing on draw card logic.");
                await effect.Execute(context);
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
        if (context.Event.GameEventPayload is not PlayerEventData playerData)
        {
            Debug.LogError($"OnDiscardCard expected PlayerEventData but got {context.Event.GameEventPayload?.GetType().Name ?? "null"}.");
            return;
        }
        foreach (var target in targetThatDiscardedCard.GetTargets(context))
        {
            if (target == playerData.Player)
            {
                Debug.Log($"Card discarded by {target}, executing on discard card logic.");
                await effect.Execute(context);
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
        if (gameEvent.Type != GameEventType.OnActivateEffectEvent) return false;
        if (gameEvent.GameEventPayload is not EffectFieldEventData fieldData)
        {
            Debug.LogError($"OnEffectFieldIsActivated expected EffectFieldEventData but got {gameEvent.GameEventPayload?.GetType().Name ?? "null"}.");
            return false;
        }
        return fieldData.Position == binding.EffectIndex;
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
        if (gameEvent.Type != GameEventType.OnDeactivateEffectEvent) return false;
        if (gameEvent.GameEventPayload is not EffectFieldEventData fieldData)
        {
            Debug.LogError($"OnEffectFieldIsDeActivated expected EffectFieldEventData but got {gameEvent.GameEventPayload?.GetType().Name ?? "null"}.");
            return false;
        }
        return fieldData.Position == binding.EffectIndex;
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
        if (gameEvent.GameEventPayload is not StatusEffectEventData statusData)
        {
            Debug.LogError($"OnStatusEffectApplied expected StatusEffectEventData but got {gameEvent.GameEventPayload?.GetType().Name ?? "null"}.");
            return false;
        }
        int mask = 1 << (int)statusData.StatusEffect.Data.effectName;
        return ((int)filter & mask) != 0 || filter == StatusEffectMask.All;
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
        if (gameEvent.GameEventPayload is not StatusEffectEventData statusData)
        {
            Debug.LogError($"OnStatusEffectRemoved expected StatusEffectEventData but got {gameEvent.GameEventPayload?.GetType().Name ?? "null"}.");
            return false;
        }
        int mask = 1 << (int)statusData.StatusEffect.Data.effectName;
        return ((int)filter & mask) != 0 || filter == StatusEffectMask.All;
    }
}
