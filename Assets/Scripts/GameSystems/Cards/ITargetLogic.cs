using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;

public readonly struct EffectContext
{
    public readonly CardInstance Instance;
    public readonly GameEvent Event;

    // Set only while a status effect's own triggers are firing, so those
    // triggers can reach the effect (and its Source) that owns them.
    public readonly StatusEffectInstance ActiveStatusEffect;

    public EffectContext(CardInstance instance, GameEvent gameEvent = default,
        StatusEffectInstance activeStatusEffect = null)
    {
        Instance = instance;
        Event = gameEvent;
        ActiveStatusEffect = activeStatusEffect;
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

    protected static Portal[] GetOpposingPortals(EffectContext context)
    {
        if (context.Instance is not FieldableCardInstance fieldCtx || fieldCtx.Board == null) return Array.Empty<Portal>();
        var lanes = fieldCtx.Board.lanes;
        var portals = new Portal[lanes.Length];
        for (int i = 0; i < lanes.Length; i++)
            portals[i] = fieldCtx.Owner.playerSide == PlayerSide.Left ? lanes[i].RightPortal : lanes[i].LeftPortal;
        return portals;
    }

    protected static Portal[] GetOwnPortals(EffectContext context)
    {
        if (context.Instance is not FieldableCardInstance fieldCtx || fieldCtx.Board == null) return Array.Empty<Portal>();
        var lanes = fieldCtx.Board.lanes;
        var portals = new Portal[lanes.Length];
        for (int i = 0; i < lanes.Length; i++)
            portals[i] = fieldCtx.Owner.playerSide == PlayerSide.Left ? lanes[i].LeftPortal : lanes[i].RightPortal;
        return portals;
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

// Resolves to the card that applied the currently firing status effect (the
// plague carrier, the attacker, ...). Only valid inside a status effect's own
// triggers. Pair with IsAlly to branch on whether the applier was friend or foe
// relative to the host minion.
[Serializable]
public class StatusEffectSourceTarget : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        if (context.ActiveStatusEffect?.Source is ITargetable targetable)
            return new List<ITargetable> { targetable };

        Debug.LogWarning(
            "StatusEffectSourceTarget found no targetable source — only valid inside a status effect's triggers, and the applier must be a minion/hero.");
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

// The frontmost enemy minion in each lane ADJACENT to the acting card's lane;
// its own lane is skipped. This is a cleave's splash: the acting minion already
// hits the lane it clashes in through normal combat, and this spills that blow
// into the fights either side of it. Empty or fully-stealthed neighbouring
// lanes are simply not hit — splash never reaches past the minions to the hero.
[Serializable]
public class NeighbouringLanesFirstTarget : ITargetLogic
{
    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        var targets = new List<ITargetable>();
        if (context.Instance is not FieldableCardInstance fieldCtx || fieldCtx.Lane == null) return targets;

        Portal[] portals = GetOpposingPortals(context); // indexed by lane
        for (int i = 0; i < portals.Length; i++)
        {
            if (Mathf.Abs(i - fieldCtx.Lane.LaneIndex) != 1) continue;

            MinionInstance target = portals[i]?.GetFirstTargetableMinion();
            if (target != null && target.IsAlive) targets.Add(target);
        }

        return targets;
    }
}

[Serializable]
public class AllLanesFirstTargetInEach : ITargetLogic
{
    //TODO currently hardcoded to only return first minion in each lane

    protected override List<ITargetable> ResolveTargets(EffectContext context)
    {
        Debug.LogWarning("AllLanesFiltered currently only returns the first minion in each lane. Implement full filtering logic.");
        var targets = new List<ITargetable>();
        var portals = GetOpposingPortals(context);
        foreach (var portal in portals)
        {
            ITargetable target = portal.GetFirstTargetableMinion();
            if (target != null) targets.Add(target);
        }
        if (targets.Count == 1)// Fallback to hero if lane is empty, to ensure we always have 3 targets for value calculations.
        {
            targets.Add(context.Instance
                .Opponent);
            targets.Add(context.Instance
                .Opponent);
        }else if (targets.Count == 2)
        {
            targets.Add(context.Instance
                .Opponent);
        }
        else if (targets.Count == 0)
        {
            targets.Add(context.Instance
                .Opponent);
            targets.Add(context.Instance
                .Opponent);
            targets.Add(context.Instance
                .Opponent);
        }
        return targets;
    }
}
