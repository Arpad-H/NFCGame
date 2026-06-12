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

// Template method: subclasses resolve their raw target set in ResolveTargets();
// the serialized filter chain is then applied uniformly here, so every target
// logic supports filters (ByName, HasStatusEffects, ExcludeStatusEffects, ...).
[Serializable]
public abstract class ITargetLogic
{
    [SerializeReference] [SubclassSelector]
    public List<ITargetFilter> filters;

    public List<ITargetable> GetTargets(EffectContext context)
    {
        var targets = ResolveTargets(context);
        if (filters == null) return targets;

        foreach (var f in filters)
        {
            if (f != null) targets = f.Apply(targets, context);
        }

        return targets;
    }

    protected abstract List<ITargetable> ResolveTargets(EffectContext context);

    // Shared helper: the acting card's portal (own side of its lane).
    protected static Portal GetOwnPortal(EffectContext context)
    {
        if (context.Instance is not FieldableCardInstance fieldCtx || fieldCtx.Lane == null) return null;
        return fieldCtx.Owner.playerSide == PlayerSide.Left
            ? fieldCtx.Lane.LeftPortal
            : fieldCtx.Lane.RightPortal;
    }

    // Shared helper: the enemy portal directly across the acting card's lane.
    protected static Portal GetOpposingPortal(EffectContext context)
    {
        if (context.Instance is not FieldableCardInstance fieldCtx || fieldCtx.Lane == null) return null;
        return fieldCtx.Owner.playerSide == PlayerSide.Left
            ? fieldCtx.Lane.RightPortal
            : fieldCtx.Lane.LeftPortal;
    }
}

[Serializable]
public class EnemyHeroTarget : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        return new List<ITargetable> { context.Instance.Opponent };
    }
}

[Serializable]
public class OwnerHeroTarget : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        return new List<ITargetable> { context.Instance.Owner };
    }
}

[Serializable]
public class DamageSourceTarget : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        // Works for the interception payload (DamageEventData) and the queued
        // OnDamaged/OnKilled reaction payload (SourceEventData).
        CardInstance source = context.Event.GameEventPayload switch
        {
            DamageEventData damageData => damageData.Source,
            SourceEventData sourceData => sourceData.Source,
            _ => null,
        };

        if (source is ITargetable targetable) return new List<ITargetable> { targetable };

        Debug.LogError(
            $"DamageSourceTarget could not resolve a source from payload {context.Event.GameEventPayload?.GetType().Name ?? "null"}.");
        return new List<ITargetable>();
    }
}

[Serializable]
public class Default : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        ITargetable target = null;
        if (context.Instance is FieldableCardInstance fieldCtx && fieldCtx.Lane != null)
        {
            var portal = context.Instance.Opponent.playerSide == PlayerSide.Left
                ? fieldCtx.Lane.LeftPortal
                : fieldCtx.Lane.RightPortal;
            // Stealthed minions can't be picked by default attack targeting.
            target = portal.GetFirstTargetableMinion();
        }

        if (target == null) target = context.Instance.Opponent;
        return new List<ITargetable> { target };
    }
}

// Extracts targets from OnAttack / OnAboutToAttack events (payload is AttackEventData).
[Serializable]
public class EventPayloadTarget : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();

        if (context.Event.GameEventPayload is not AttackEventData attackData)
        {
            Debug.LogError($"EventPayloadTarget expected AttackEventData but got {context.Event.GameEventPayload?.GetType().Name ?? "null"}.");
            return targets;
        }
        targets.AddRange(attackData.Targets);
        return targets;
    }
}

[Serializable]
public class OwnLane : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();
        var portal = GetOwnPortal(context);
        if (portal != null) targets.AddRange(portal.GetAllMinionsInPortal());
        return targets;
    }
}

[Serializable]
public class OpposingLane : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();
        var portal = GetOpposingPortal(context);
        if (portal != null) targets.AddRange(portal.GetAllMinionsInPortal());
        return targets;
    }
}

[Serializable]
public class AllMinions : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
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
    protected override List<ITargetable> ResolveTargets(EffectContext context)
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
    protected override List<ITargetable> ResolveTargets(EffectContext context)
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
    protected override List<ITargetable> ResolveTargets(EffectContext context)
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
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();
        var portal = GetOwnPortal(context);
        if (portal == null) return targets;
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
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();
        var portal = GetOwnPortal(context);
        if (portal == null) return targets;
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
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        return new List<ITargetable> { context.Instance as ITargetable };
    }
}
