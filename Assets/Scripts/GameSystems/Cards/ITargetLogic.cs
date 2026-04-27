using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;
public readonly struct EffectContext
{
    public readonly CardInstance Instance;
    public readonly object EffectContextPayload;

    public EffectContext(CardInstance instance, object effectContextPayload = null)
    {
        Instance = instance;
        EffectContextPayload = effectContextPayload;
    }
}
public interface ITargetLogic
{
    List<ITargetable> GetTargets(EffectContext context);
}

[Serializable]
public class EnemyHeroTarget : ITargetLogic
{
    public List<ITargetable> GetTargets(EffectContext context)
    {
        return new List<ITargetable> { context.Instance.Opponent };
    }
}
[Serializable]
public class OwnerHeroTarget : ITargetLogic
{
    public List<ITargetable> GetTargets(EffectContext context)
    {
        return new List<ITargetable> { context.Instance.Owner };
    }
}
[Serializable]
public class DamageSourceTarget : ITargetLogic
{
    public List<ITargetable> GetTargets(EffectContext context)
    {
        if (context.EffectContextPayload is GameEvent dmg )
        {
            if (dmg.GameEventPayload is ITargetable src)
            {
                    return new List<ITargetable> { src };
            }
            
        }
        return new List<ITargetable>();
    }
}

[Serializable]
public class Default : ITargetLogic
{
    public List<ITargetable> GetTargets(EffectContext context)
    {
        ITargetable target = null;
        if (context.Instance is FieldableCardInstance fieldCtx && fieldCtx.Lane != null)
        {
            if (context.Instance.Opponent.playerSide == PlayerSide.Left)
            {
                target = fieldCtx.Lane.LeftPortal.GetMinion(0);
            }
            else
            {
                target = fieldCtx.Lane.RightPortal.GetMinion(0);
            }
        }

        if (target == null) target = context.Instance.Opponent;
        return new List<ITargetable> { target };
    }
}
//TODO on draw card, target behind and sides,rotate portals, scare minion to the back, modify stats, spawn a certain card, trigger all effects of type