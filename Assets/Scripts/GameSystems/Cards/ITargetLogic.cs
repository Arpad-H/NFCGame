using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;

public interface ITargetLogic
{
    List<ITargetable> GetTargets(CardContext context);
}

[Serializable]
public class EnemyHeroTarget : ITargetLogic
{
    public List<ITargetable> GetTargets(CardContext context)
    {
        return new List<ITargetable> { context.Opponent };
    }
}
[Serializable]
public class OwnerHeroTarget : ITargetLogic
{
    public List<ITargetable> GetTargets(CardContext context)
    {
        return new List<ITargetable> { context.Owner };
    }
}

[Serializable]
public class Default : ITargetLogic
{
    public List<ITargetable> GetTargets(CardContext context)
    {
        ITargetable target = null;
        if (context is FieldableCardContext fieldCtx && fieldCtx.Lane != null)
        {
            if (context.Opponent.playerSide == PlayerSide.Left)
            {
                target = fieldCtx.Lane.LeftPortal.GetMinion(0)?.cardInstance;
            }
            else
            {
                target = fieldCtx.Lane.RightPortal.GetMinion(0)?.cardInstance;
            }
        }

        if (target == null) target = context.Opponent;
        return new List<ITargetable> { target };
    }
}