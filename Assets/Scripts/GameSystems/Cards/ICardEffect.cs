using System.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
using GameSystems;
using GameSystems.Cards;
using UnityEngine.Serialization;


public interface ICardEffect
{
    Task Execute(EffectContext context);
}

[System.Serializable]
public class DefaultAttackEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    private ITargetLogic targetLogic = new Default();

    public async Task Execute(EffectContext context)
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


        if (minion.SourcePortal != null)
        {
            var visualizer = minion.SourcePortal.GetVisualizer(minion);
            if (visualizer != null && targets.Count > 0)
            {
                // Simple DOTween sequence for attacking the first target visually
                var target = targets[0]; //thers only one for default attack
                Vector3 originalPos = visualizer.transform.position;
                Vector3 targetPos = originalPos;

                if (target is MinionInstance targetMinion && targetMinion.SourcePortal != null)
                {
                    var targetVisualizer = targetMinion.SourcePortal.GetVisualizer(targetMinion);
                    if (targetVisualizer != null)
                    {
                        targetPos = targetVisualizer.transform.position;
                    }
                }
                else if (target is Player playerTarget)
                {
                    targetPos = playerTarget.healthText.transform.position; // rough proxy
                }

                // Punch animation: go to target and back
                await visualizer.transform.DOMove(Vector3.Lerp(originalPos, targetPos, 0.6f), 0.2f)
                    .SetEase(Ease.InCubic).AsyncWaitForCompletion();
                await target.TakeDamage(new DamageEventData(amount, context.Instance));
                Debug.Log($"context: {context}, target: {target}, damage: {amount}");
                await visualizer.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutCubic).AsyncWaitForCompletion();
            }
        }

        // Emit the attack event BEFORE passing damage, with targets in the payload
        await minion.HandleEvent(new GameEvent(GameEventType.OnAttack, minion, targets));
    }
}

[System.Serializable]
public class DamageEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    [SerializeReference] [SubclassSelector]
    public ICalculateValueLogic amountLogic;

    private int damageAmount;


    public DamageEffect()
    {
    }

    public DamageEffect(int amount, ITargetLogic targetLogic)
    {
        this.damageAmount = amount;
        this.targetLogic = targetLogic;
    }

    public async Task Execute(EffectContext context)
    {
        if (targetLogic == null)
        {
            Debug.LogError("No target logic assigned for damage effect, skipping execution.");
            return;
        }

        var targets = targetLogic.GetTargets(context);
        foreach (var t in targets)
        {
            damageAmount = amountLogic.CalculateValue(context);
            await t.TakeDamage(new DamageEventData(damageAmount, context.Instance));
            Debug.Log($"context: {context.Instance}, target: {t}, damage: {damageAmount}");
        }
    }
}

[System.Serializable]
public class DrawCardEffect : ICardEffect
{
    public int count;

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public async Task Execute(EffectContext context)
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
                await player.DrawCard(count);
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

    public async Task Execute(EffectContext context)
    {
        var targets = targetLogic.GetTargets(context);
        foreach (var t in targets)
        {
            if (t is IPlayerTargetable player)
            {
                await player.DiscardCard(count);
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
    public async Task Execute(EffectContext context)
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
                        Debug.Log(
                            $"New target {newTarget} is the same as original target, skipping to avoid infinite loop.");
                        continue;
                    }

                    if (newTarget is ITargetable targetable)
                    {
                        var redirectedDamage =
                            new DamageEventData(damageData.Amount, damageData.Source ?? context.Instance);
                        await targetable.TakeDamage(redirectedDamage);
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
                    Debug.Log(
                        $"New target {newTarget} is the same as original target, skipping to avoid infinite loop.");
                    continue;
                }

                if (newTarget is IGameEventReceiver receiver)
                {
                    await receiver.HandleEvent(redirectedEvent);
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
public class TriggerEventEffect : ICardEffect
{
    public GameEventType eventType;

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetToTriggerEventOn;

    public async Task Execute(EffectContext context)
    {
        var targets = targetToTriggerEventOn.GetTargets(context);

        foreach (var t in targets)
        {
            if (t is IGameEventReceiver receiver)
            {
                await receiver.HandleEvent(new GameEvent(eventType, null));
            }
        }

        Debug.Log($"Triggering event {eventType} on targets: {string.Join(", ", targets)}");
    }
}

[System.Serializable]
public class TriggerAttackEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    private ITargetLogic whoAttacks = new Default();

    [SerializeReference] [SubclassSelector]
    private ITargetLogic attacksWho = new Default();

    public async Task Execute(EffectContext context)
    {
        var attackers = whoAttacks.GetTargets(context);
        var defenders = attacksWho.GetTargets(context);

        foreach (var t in attackers)
        {
            if (t is MinionInstance minionAttacker)
            {
                var amount = minionAttacker.CurrentAttack;
                foreach (var defender in defenders)
                {
                    //TODO Replace with more sophisticated reusable tween function
                    if (minionAttacker.SourcePortal != null)
                    {
                        var visualizer = minionAttacker.SourcePortal.GetVisualizer(minionAttacker);
                        if (visualizer != null && defenders.Count > 0)
                        {
                            // Simple DOTween sequence for attacking the first target visually
                            var target = defenders[0];
                            Vector3 originalPos = visualizer.transform.position;
                            Vector3 targetPos = originalPos;

                            if (target is MinionInstance targetMinion && targetMinion.SourcePortal != null)
                            {
                                var targetVisualizer = targetMinion.SourcePortal.GetVisualizer(targetMinion);
                                if (targetVisualizer != null)
                                {
                                    targetPos = targetVisualizer.transform.position;
                                }
                            }
                            else if (target is Player playerTarget)
                            {
                                targetPos = playerTarget.healthText.transform.position; // rough proxy
                            }

                            // Punch animation: go to target and back
                            await visualizer.transform.DOMove(Vector3.Lerp(originalPos, targetPos, 0.6f), 0.2f)
                                .SetEase(Ease.InCubic).AsyncWaitForCompletion();
                            await defender.TakeDamage(new DamageEventData(amount, minionAttacker));
                            await visualizer.transform.DOMove(originalPos, 0.3f).SetEase(Ease.OutCubic)
                                .AsyncWaitForCompletion();
                        }
                    }

                    Debug.Log(
                        $"context: {context}, target: {defender}, damage: {amount} from {minionAttacker.SourceCard.cardName}");
                }
            }
        }
    }
}

[System.Serializable]
public class ModifyStatsEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    ITargetLogic targetToTriggerEffectOn;

    [SerializeReference] [SubclassSelector]
    ICalculateValueLogic hp;

    [SerializeReference] [SubclassSelector]
    ICalculateValueLogic attack;

    public async Task Execute(EffectContext context)
    {
        var targets = targetToTriggerEffectOn.GetTargets(context);
        foreach (var t in targets)
        {
            int hpVal = hp != null ? hp.CalculateValue(context) : 0;
            int attackVal = attack != null ? attack.CalculateValue(context) : 0;
            await t.ModifyStat(MinionStats.Health, hpVal);
            await t.ModifyStat(MinionStats.Attack, attackVal);
            Debug.Log(
                $"Modifying stats of {t} by {hpVal} health and {attackVal} attack. Context: {context}");
        }
    }
}

[System.Serializable]
public class ApplyStatusEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetToPutStatusEffectOn;

    // Instead of complex abstract type matching, just reference the SO directly
    public StatusEffectData statusEffectToApply;
    public int duration = 1;

    public async Task Execute(EffectContext context)
    {
        var targets = targetToPutStatusEffectOn.GetTargets(context);
        foreach (var t in targets)
        {
            if (t is MinionInstance minion)
            {
                // 1. Create the simple runtime wrapper
                StatusEffectInstance newEffect = new StatusEffectInstance(statusEffectToApply, duration);

                // 2. Add it to the minion
                minion.ApplyStatusEffect(newEffect);
                Debug.Log($"Applying status effect {statusEffectToApply.effectName} to {minion}.");
            }
        }

        await Task.CompletedTask;
    }
}

[System.Serializable]
public class RemoveStatusEffect : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetToRemoveStatusEffectOn;

    public StatusEffectData statusEffectToRemove;

    public async Task Execute(EffectContext context)
    {
        var targets = targetToRemoveStatusEffectOn.GetTargets(context);
        foreach (var t in targets)
        {
            if (t is MinionInstance minion)
            {
                await minion.RemoveStatusEffect(statusEffectToRemove);
                Debug.Log($"Removing status effect {statusEffectToRemove.effectName} from {minion}.");
            }
        }

        await Task.CompletedTask;
    }
}

[System.Serializable]
public class CheckCondition : ICardEffect
{
    [SerializeReference] [SubclassSelector]
    IConditionalCheck condition;

    [SerializeReference] [SubclassSelector]
    ICardEffect effectIfTrue;

    [SerializeReference] [SubclassSelector]
    ICardEffect effectIfFalse;


    public Task Execute(EffectContext context)
    {
        if (condition.CheckCondition(context))
        {
            Debug.Log($"Condition {condition} is true, executing effect {effectIfTrue}");
            return effectIfTrue.Execute(context);
        }
        else
        {
            Debug.Log($"Condition {condition} is false, executing effect {effectIfFalse}");
            return effectIfFalse.Execute(context);
        }
    }
}

[System.Serializable]
public class ReturnToHand : ICardEffect
{
    public Task Execute(EffectContext context)
    {
        if (context.Instance is FieldableCardInstance fieldableCardInstance)
        {
            return fieldableCardInstance.ReturnToHand();
        }
        else
        {
            Debug.LogError($"ReturnToHand effect can only be applied to FieldableCardInstances, but got {context.Instance.GetType()}");
            return Task.CompletedTask;
        }
    }
}

[System.Serializable]
public class CustomLogicEffect : ICardEffect //Escape hatch for  complex logic without the lego bricks
{
    public async Task Execute(EffectContext context)
    {
        Debug.Log("Executing hyper-complex chaos logic");
    }
}