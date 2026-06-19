using GameSystems;
using UnityEngine;

public interface IConditionalCheck
{
    public bool CheckCondition(EffectContext context);
}
// True while the chosen stat of any resolved target is below the threshold.
// Gates capped growth ("+1 HP per round until 10 HP") and low-HP triggers.
[System.Serializable]
public class StatBelowThreshold : IConditionalCheck
{
    public enum Stat { CurrentHealth, MaxHealth, Attack }

    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public Stat stat;
    public int threshold;

    public bool CheckCondition(EffectContext context)
    {
        foreach (var target in targetLogic.GetTargets(context))
        {
            if (target is not MinionInstance minion) continue;

            int value = stat switch
            {
                Stat.CurrentHealth => minion.CurrentHealth,
                Stat.MaxHealth => minion.MaxHealth,
                Stat.Attack => minion.CurrentAttack,
                _ => 0,
            };

            if (value < threshold) return true;
        }

        return false;
    }
}

// True when the in-flight damage was produced by the given source type.
// Use on OnAboutToTakeDamage: e.g. "blocks all damage that does not originate
// from minion attacks" = if NOT Attack → PreventDamageEffect.
[System.Serializable]
public class DamageSourceTypeIs : IConditionalCheck
{
    public DamageSourceType sourceType;

    public bool CheckCondition(EffectContext context)
    {
        return context.Event.GameEventPayload is DamageEventData damageData
               && damageData.SourceType == sourceType;
    }
}

// True when the card the event happened TO (Event.EffectSource) is among the
// resolved targets. Items listening to broadcasts use this to react only to
// their own holder: "if the HOLDER is attacked/killed/healed ..." =
// EventSourceMatches { targetLogic = ItemHolder }.
[System.Serializable]
public class EventSourceMatches : IConditionalCheck
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public bool CheckCondition(EffectContext context)
    {
        var source = context.Event.EffectSource;
        if (source == null) return false;

        foreach (var target in targetLogic.GetTargets(context))
        {
            if (ReferenceEquals(target, source)) return true;
        }

        return false;
    }
}

// True when the entity that CAUSED the event (the payload's Source — attacker,
// healer, killer) is among the resolved targets. The lifesteal brick: on
// OnDamaged, EventPayloadSourceMatches { ItemHolder } means "my holder dealt
// this damage" → HealEffect(EventAmountValue) on the holder.
[System.Serializable]
public class EventPayloadSourceMatches : IConditionalCheck
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public bool CheckCondition(EffectContext context)
    {
        CardInstance source = context.Event.GameEventPayload switch
        {
            DamageEventData damageData => damageData.Source,
            HealEventData healData => healData.Source,
            SourceEventData sourceData => sourceData.Source,
            _ => null,
        };

        if (source == null) return false;

        foreach (var target in targetLogic.GetTargets(context))
        {
            if (ReferenceEquals(target, source)) return true;
        }

        return false;
    }
}

// True when ALL resolved targets belong to the same owner as the context instance (allies).
// Set isAlly = false to invert: returns true only when all targets are enemies.
[System.Serializable]
public class IsAlly : IConditionalCheck
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public bool isAlly = true;

    public bool CheckCondition(EffectContext context)
    {
        foreach (var target in targetLogic.GetTargets(context))
        {
            if (target is not CardInstance card) continue;
            bool allied = card.Owner == context.Instance.Owner;
            if (allied != isAlly) return false;
        }

        return true;
    }
}

[System.Serializable]
public class HasStatusEffect : IConditionalCheck
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    [SerializeReference] 
    public StatusEffectMask statusEffect;

    public bool CheckCondition(EffectContext context)
    {
        var targets = targetLogic.GetTargets(context);
        foreach (var target in targets)
        {
            if (target is MinionInstance minion)
            {
                foreach (StatusEffectType type in StatusEffectExtension.MaskToTypes(statusEffect))
                {
                    if (minion.HasStatusEffect(type))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}


