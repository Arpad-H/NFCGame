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
//
// Note on OnCombatResolution: it reaches only a minion that actually swings,
// at the instant its blow lands (see Board.ResolveCombat). Put a card's extra
// combat behaviour here — a stunned, back-row or dead minion never fires it.
[System.Serializable]
public class OnGameEvent : IEventTrigger
{
    public GameEventType type;

    // Only run the effect when the event happened TO this card itself
    // (Event.EffectSource == the bound card). OnDamaged/OnHealed/OnKilled are
    // board-wide broadcasts, so without this a "when damaged: X" card reacts
    // to EVERY damage event anywhere on the board. Leave off for triggers that
    // genuinely watch the whole board and for sourceless events (round start/
    // end have no EffectSource and would never pass the check).
    public bool onlySelf;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    public async Task Execute(EffectContext context)
    {
        if (onlySelf && !ReferenceEquals(context.Event.EffectSource, context.Instance)) return;

        if (effect == null)
        {
            Debug.LogError($"No effect assigned to OnGameEvent ({type}), skipping execution.");
            return;
        }
        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        if (gameEvent.Type == GameEventType.OnActivateEffectEvent ||
            gameEvent.Type == GameEventType.OnDeactivateEffectEvent)
        {
            Debug.LogWarning(
                $"OnGameEvent is configured with {gameEvent.Type}, but that event requires a field-position check. " +
                "Use OnEffectFieldIsActivated / OnEffectFieldIsDeActivated instead.");
            return false;
        }
        return gameEvent.Type == type;
    }
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

// Fires on a matching event at most maxTriggers times per card instance
// (counted in the binding's StateSlot). Covers "do once ever" (maxTriggers = 1,
// e.g. revive once, spawn golem once) and per-round limits via resetEachRound
// (e.g. "only X once per round" — the count zeroes when a round ends).
// For broadcast events (OnDamaged/OnKilled/...) tick onlySelf, or the charge
// is consumed by the FIRST matching event anywhere on the board.
[System.Serializable]
public class TriggerNTimes : IEventTrigger
{
    public GameEventType type;
    public int maxTriggers = 1;
    public bool resetEachRound;

    // Count (and fire) only when the event happened TO the bound card itself —
    // same semantics as OnGameEvent.onlySelf, but checked before the charge is
    // spent. Relies on binding.Owner, stamped by the dispatch loops.
    public bool onlySelf;

    // Optional gate checked BEFORE the charge is spent: the trigger neither
    // fires nor counts unless it passes. This is how a once-only charge is
    // scoped to a real occurrence rather than any type-matching event — e.g.
    // Beaked Mask's "resurrect once if the HOLDER dies INFECTED" only burns
    // its charge when the dying card is the holder AND the holder has Plague
    // (onlySelf can't express that: for an item binding the owner is the item,
    // not the holder). Evaluated against the binding's owner as context.
    [SerializeReference] [SubclassSelector]
    public IConditionalCheck countCondition;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect;

    // Per-instance state held in TriggerBinding.StateSlot.
    private class State
    {
        public int Count;
    }

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError($"No effect assigned to TriggerNTimes ({type}), skipping execution.");
            return;
        }

        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        if (binding.StateSlot is not State state)
        {
            binding.StateSlot = state = new State();
        }

        if (resetEachRound && gameEvent.Type == GameEventType.OnRoundEnd)
        {
            state.Count = 0;
            // OnRoundEnd is only the reset signal unless it's also the watched type.
            if (type != GameEventType.OnRoundEnd) return false;
        }

        if (gameEvent.Type != type) return false;
        if (onlySelf && !ReferenceEquals(gameEvent.EffectSource, binding.Owner)) return false;
        if (state.Count >= maxTriggers) return false;

        // The charge is a scarce resource: don't spend it on an event the
        // wrapped effect wouldn't act on.
        if (countCondition != null &&
            !countCondition.CheckCondition(new EffectContext(binding.Owner, gameEvent))) return false;

        state.Count++;
        return true;
    }
}

// Enforces "can only receive damage from N distinct sources per round".
// Watches OnAboutToTakeDamage: damage from a source already seen this round
// passes through; once the per-round source limit is reached, damage from any
// NEW source fires the effect (typically PreventDamageEffect). The seen-set
// lives in the binding's StateSlot and clears on OnRoundEnd.
[System.Serializable]
public class LimitDamageSourcesPerRound : IEventTrigger
{
    public int maxSources = 1;

    [SerializeReference] [SubclassSelector]
    ICardEffect effect = new PreventDamageEffect();

    // Per-instance state held in TriggerBinding.StateSlot.
    private class State
    {
        public readonly HashSet<CardInstance> SourcesThisRound = new();
    }

    public async Task Execute(EffectContext context)
    {
        if (effect == null)
        {
            Debug.LogError("No effect assigned to LimitDamageSourcesPerRound, skipping execution.");
            return;
        }

        await effect.Execute(context);
    }

    public bool CanTrigger(GameEvent gameEvent, TriggerBinding binding)
    {
        if (binding.StateSlot is not State state)
        {
            binding.StateSlot = state = new State();
        }

        if (gameEvent.Type == GameEventType.OnRoundEnd)
        {
            state.SourcesThisRound.Clear();
            return false;
        }

        if (gameEvent.Type != GameEventType.OnAboutToTakeDamage) return false;
        if (gameEvent.GameEventPayload is not DamageEventData damageData) return false;

        // Sourceless damage can't be attributed — let it through untouched.
        if (damageData.Source == null) return false;

        // A source we already accepted this round may keep dealing damage.
        if (state.SourcesThisRound.Contains(damageData.Source)) return false;

        if (state.SourcesThisRound.Count < maxSources)
        {
            state.SourcesThisRound.Add(damageData.Source);
            return false;
        }

        // Limit reached and this is a new source → fire (prevent the damage).
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

// Filtered variant: fires only when the applied status effect matches the bitmask.
// Use OnGameEvent { type = OnStatusEffectApplied } instead if any status effect should trigger.
[System.Serializable]
public class OnSpecificStatusEffectApplied : IEventTrigger
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
            Debug.LogError($"OnSpecificStatusEffectApplied expected StatusEffectEventData but got {gameEvent.GameEventPayload?.GetType().Name ?? "null"}.");
            return false;
        }
        int mask = 1 << (int)statusData.StatusEffect.Data.effectName;
        return ((int)filter & mask) != 0 || filter == StatusEffectMask.All;
    }
}

// Filtered variant: fires only when the removed status effect matches the bitmask.
// Use OnGameEvent { type = OnStatusEffectRemoved } instead if any status effect should trigger.
[System.Serializable]
public class OnSpecificStatusEffectRemoved : IEventTrigger
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
            Debug.LogError($"OnSpecificStatusEffectRemoved expected StatusEffectEventData but got {gameEvent.GameEventPayload?.GetType().Name ?? "null"}.");
            return false;
        }
        int mask = 1 << (int)statusData.StatusEffect.Data.effectName;
        return ((int)filter & mask) != 0 || filter == StatusEffectMask.All;
    }
}
