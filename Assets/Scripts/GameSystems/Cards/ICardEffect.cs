using System.Collections.Generic;
using GameSystems;
using NUnit.Framework;
using UnityEngine;


public interface ICardEffect
{
    void Execute(EffectContext context);
}

[System.Serializable]
public class DefaultAttackEffect : ICardEffect
{
    private ITargetLogic targetLogic = new Default();

    public void Execute(EffectContext context)
    {
        if (context.Instance is not MinionInstance minion)
        {
            Debug.LogError(
                $"DefaultAttackEffect expected MinionInstance but got {context.Instance.GetType()}, skipping execution.");
            return;
        }

        if (context.Instance is FieldableCardInstance fieldableCardInstance)
        {
            if (fieldableCardInstance.Lane == null)
            {
                Debug.LogError("DefaultAttackEffect requires the card to be on a lane, skipping execution.");
                return;
            }

            if (fieldableCardInstance.SourcePortal.GetMinionPosition(fieldableCardInstance) != 0)
            {
                Debug.Log(
                    "DefaultAttackEffect requires the card to be in the front position of the lane, skipping execution.");
                return;
            }
        }

        int amount = minion.CurrentAttack;
        var targets = targetLogic.GetTargets(context);

        foreach (var t in targets)
        {
            t.TakeDamage(new DamageEventData(amount, context.Instance));
            Debug.Log($"context: {context}, target: {t}, damage: {amount}");
        }
    }
}

[System.Serializable]
public class DamageEffect : ICardEffect
{
    public int amount;

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public DamageEffect()
    {
    }

    public DamageEffect(int amount, ITargetLogic targetLogic)
    {
        this.amount = amount;
        this.targetLogic = targetLogic;
    }

    public void Execute(EffectContext context)
    {
        if (targetLogic == null)
        {
            Debug.LogError("No target logic assigned for damage effect, skipping execution.");
            return;
        }

        var targets = targetLogic.GetTargets(context);
        foreach (var t in targets)
        {
            t.TakeDamage(new DamageEventData(amount, context.Instance));
            Debug.Log($"context: {context.Instance}, target: {t}, damage: {amount}");
        }
    }
}

[System.Serializable]
public class DrawCardEffect : ICardEffect
{
    public int count;

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public void Execute(EffectContext context)
    {
        if (targetLogic == null)
        {
            Debug.LogError("No target logic assigned for draw effect, skipping execution.");
            return;
        }

        var targets = targetLogic.GetTargets(context);
        foreach (var t in targets)
        {
            if (t is IPlayerTargetable player)
            {
                player.DrawCard(count);
            }
        }

        Debug.Log($"Drawing {count} cards");
    }
}

[System.Serializable]
public class DiscardCardEffect : ICardEffect
{
    public int count;
    public bool randomDiscard; //If true, discard random cards. If false, discard from the end of the hand.

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public void Execute(EffectContext context)
    {
        var targets = targetLogic.GetTargets(context);
        foreach (var t in targets)
        {
            if (t is IPlayerTargetable player)
            {
                player.DiscardCard(count);
            }
        }

        Debug.Log($"Discarding {count} cards {(randomDiscard ? "randomly" : "chosen by player")}");
        Debug.LogWarning("RANDOM DISCARD NOT IMPLEMENTED");
    }
}

[System.Serializable]
public class RedirectEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic newTargetLogic;
    
    // [SerializeReference] [SubclassSelector]
    // public ITargetLogic fallbackBehavior = new SelfTarget(); // fallback if newTargetLogic returns no targets
    public void Execute(EffectContext context)
    {
        if (context.EffectContextPayload is GameEvent gameEvent)
        {
            var originalTarget = context.Instance;
            var newTargets = newTargetLogic.GetTargets(context);

         
          
            //TODO This only works for damage events right now, need a more general solution for other event types 
            if (newTargets.Count > 0 && gameEvent.GameEventPayload is DamageEventData damageData)
            {
                damageData.IsPrevented = true;

                foreach (var newTarget in newTargets)
                {
                    if (newTarget == originalTarget)
                    {
                        Debug.Log($"New target {newTarget} is the same as original target, skipping to avoid infinite loop.");
                        continue;
                    }

                    if (newTarget is ITargetable targetable)
                    {
                        var redirectedDamage = new DamageEventData(damageData.Amount, damageData.Source ?? context.Instance);
                        targetable.TakeDamage(redirectedDamage);
                    }
                }
                return;
            }
            
            // For all other events, forward the original event type and payload.
            var fieldInstance = context.Instance as FieldableCardInstance;
            var redirectedEvent = new GameEvent(gameEvent.Type, fieldInstance, gameEvent.GameEventPayload);
            
            foreach (var newTarget in newTargets)
            {
                if (newTarget == originalTarget)
                {
                   Debug.Log($"New target {newTarget} is the same as original target, skipping to avoid infinite loop.");
                    continue;
                }
                if (newTarget is IGameEventReceiver receiver)
                {
                    receiver.HandleEvent(redirectedEvent);
                }
            }

            Debug.Log(
                $"Redirecting effect from original targets {string.Join(", ", originalTarget)} to new targets {string.Join(", ", newTargets)}");
        }
        else
        {
            Debug.LogError("RedirectEffect requires a GameEvent in the EffectContextPayload, skipping execution.");
        }
    }
}

[System.Serializable]
public class CustomLogicEffect : ICardEffect //Escape hatch for  complex logic without the lego bricks
{
    public void Execute(EffectContext context)
    {
        Debug.Log("Executing hyper-complex chaos logic");
    }
}