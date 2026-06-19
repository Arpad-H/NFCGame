using UnityEngine;

namespace GameSystems.Cards
{
public interface ICalculateValueLogic
{
        int CalculateValue(EffectContext context);
}

[System.Serializable]
public class IntegerValue : ICalculateValueLogic
{
    public int value = 0;

    public int CalculateValue(EffectContext context)
    {
        return value;
    }
}
[System.Serializable]
public class NumberOfTargets : ICalculateValueLogic
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public int CalculateValue(EffectContext context)
    {
       var  targets = targetLogic.GetTargets(context);
       return targets.Count;
    }
}

// Reads the numeric amount carried by the triggering event (damage dealt,
// healing received). Enables "heal by the same amount", lifesteal, and
// reflect-exact-damage effects.
[System.Serializable]
public class EventAmountValue : ICalculateValueLogic
{
    public int CalculateValue(EffectContext context)
    {
        return context.Event.GameEventPayload switch
        {
            DamageEventData damage => damage.Amount,
            HealEventData heal => heal.Amount,
            SourceEventData source => source.Amount,
            _ => 0,
        };
    }
}

[System.Serializable]
public class MinionTotalMaxHp : ICalculateValueLogic
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public int CalculateValue(EffectContext context)
    {
        var targets = targetLogic.GetTargets(context);
        int total = 0;
        foreach (var t in targets)
        {
            if (t is MinionInstance minion)
            {
                total += minion.BaseHealth;
            }
        }

        return total;
    }
}
[System.Serializable]
public class MinionTotalBaseAttack : ICalculateValueLogic
{
    [SerializeReference] [SubclassSelector]
    public ITargetLogic targetLogic;

    public int CalculateValue(EffectContext context)
    {
        var targets = targetLogic.GetTargets(context);
        int total = 0;
        foreach (var t in targets)
        {
            if (t is MinionInstance minion)
            {
                total += minion.BaseAttack;
            }
        }

        return total;
    }
}



}