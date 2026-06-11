using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;

public readonly struct EffectContext
{
    public readonly CardInstance Instance;
    public readonly GameEvent Event;

    public EffectContext(CardInstance instance, GameEvent gameEvent = default)
    {
        Instance = instance;
        Event = gameEvent;
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
        if (context.Event.GameEventPayload is not DamageEventData damageData)
        {
            Debug.LogError($"DamageSourceTarget expected DamageEventData but got {context.Event.GameEventPayload?.GetType().Name ?? "null"}.");
            return new List<ITargetable>();
        }
        return new List<ITargetable> { damageData.Source as ITargetable };
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

// Extracts targets from OnAttack / OnAboutToAttack events (payload is AttackEventData).
[Serializable]
public class EventPayloadTarget : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();

        if (context.Event.GameEventPayload is not AttackEventData attackData)
        {
            Debug.LogError($"EventPayloadTarget expected AttackEventData but got {context.Event.GameEventPayload?.GetType().Name ?? "null"}.");
            return targets;
        }
        targets.AddRange(attackData.Targets);

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
            foreach (var m in fieldCtx.Board.GetAllMinionsOnBoard())
                itargets.Add(m);
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
            foreach (var m in fieldCtx.Board.GetAllMinionsOnBoard())
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
            foreach (var m in fieldCtx.Board.GetAllMinionsOnBoard())
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
public class MinionInFront : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();
        if (context.Instance is not FieldableCardInstance fieldCtx || fieldCtx.Lane == null) return targets;
        var portal = fieldCtx.Owner.playerSide == PlayerSide.Left
            ? fieldCtx.Lane.LeftPortal
            : fieldCtx.Lane.RightPortal;
        var minions = portal.GetAllMinionsInPortal();

        int position = -1;
        for (int i = 0; i < minions.Count; i++)
        {
            if (minions[i] == context.Instance) { position = i; break; }
        }

        if (position > 0) targets.Add(minions[position - 1]);
        return targets;
    }
}

[Serializable]
public class MinionBehind : ITargetLogic
{
    public override List<ITargetable> GetTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();
        if (context.Instance is not FieldableCardInstance fieldCtx || fieldCtx.Lane == null) return targets;
        var portal = fieldCtx.Owner.playerSide == PlayerSide.Left
            ? fieldCtx.Lane.LeftPortal
            : fieldCtx.Lane.RightPortal;
        var minions = portal.GetAllMinionsInPortal();

        int position = -1;
        for (int i = 0; i < minions.Count; i++)
        {
            if (minions[i] == context.Instance) { position = i; break; }
        }

        if (position >= 0 && position + 1 < minions.Count) targets.Add(minions[position + 1]);
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
