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

[Serializable]
public abstract class ITargetLogic
{
    [SerializeReference] [SubclassSelector]
    public List<ITargetFilter> filters;

    public abstract List<ITargetable> GetTargets(EffectContext context);
}

[Serializable]
public class EnemyHeroTarget : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        return new List<ITargetable> { context.Instance.Opponent };
    }
}

[Serializable]
public class OwnerHeroTarget : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        return new List<ITargetable> { context.Instance.Owner };
    }
}

[Serializable]
public class DamageSourceTarget : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        if (context.EffectContextPayload is GameEvent dmg )
        {
            if (dmg.GameEventPayload is DamageEventData damageEventData)
            {
                return new List<ITargetable> { damageEventData.Source as ITargetable };
            }
        }

        return new List<ITargetable>();
    }
}

[Serializable]
public class Default : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
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

[Serializable]
public class EventPayloadTarget : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();

        if (context.EffectContextPayload is GameEvent e && e.GameEventPayload is List<ITargetable> payloadTargets)
        {
            targets.AddRange(payloadTargets);
        }

        foreach (var f in filters)
        {
            if (f != null) targets = f.Apply(targets, context);
        }

        return targets;
    }
}

[Serializable]
public class OwnLane : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        var targets = GetOwnLaneTargets(context);

        foreach (var f in filters)
        {
            if (f != null) targets = f.Apply(targets, context);
        }

        return targets;
    }

    private List<ITargetable> GetOwnLaneTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();
        if (context.Instance is FieldableCardInstance fieldCtx && fieldCtx.Lane != null)
        {
            var portal = fieldCtx.Owner.playerSide == PlayerSide.Left
                ? fieldCtx.Lane.LeftPortal
                : fieldCtx.Lane.RightPortal;
            var minions = portal.GetAllMinionsInPortal();
            targets.AddRange(minions);
        }

        return targets;
    }
}

[Serializable]
public class OpposingLane : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        throw new NotImplementedException();
    }
}

[Serializable]
public class AllMinions : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        var itargets = new List<ITargetable>();
        if (context.Instance is FieldableCardInstance fieldCtx)
        {
            var targets = fieldCtx.Board.GetAllMinionsOnBoard();
            //convert to itargetable
            foreach (var m in targets)
            {
                itargets.Add(m);
            }
        }

        return itargets;
    }
}

[Serializable]
public class FriendlyMinions : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        var itargets = new List<ITargetable>();
        if (context.Instance is FieldableCardInstance fieldCtx)
        {
            var targets = fieldCtx.Board.GetAllMinionsOnBoard();
            foreach (var m in targets)
            {
                if (m.Owner == context.Instance.Owner) itargets.Add(m);
            }
        }

        return itargets;
    }
}

[Serializable]
public class EnemyMinions : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        var itargets = new List<ITargetable>();
        if (context.Instance is FieldableCardInstance fieldCtx)
        {
            var targets = fieldCtx.Board.GetAllMinionsOnBoard();
            foreach (var m in targets)
            {
                if (m.Owner != context.Instance.Owner) itargets.Add(m);
            }
        }

        return itargets;
    }
}
[Serializable]
public class ItemHolder : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();
        if (context.Instance is ItemInstance item)
        {
            targets.Add(item.ItemHolder);
        }

        return targets;
    }
}

[Serializable]
public class SelfTarget : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        return new List<ITargetable> { context.Instance as ITargetable };
    }
}