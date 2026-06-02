using GameSystems;
using UnityEngine;

public interface IConditionalCheck
{
    public bool CheckCondition(EffectContext context);
}
//TODO prob not needed. conditional is just an asnwer to the inactive effects to make sure continous effects update their logic
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