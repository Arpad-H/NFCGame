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