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
}