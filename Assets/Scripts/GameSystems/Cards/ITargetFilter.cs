
using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;

public interface ITargetFilter
{
    List<ITargetable> Apply(List<ITargetable> input, EffectContext context);
}
[Serializable]
public class ByNameFilter : ITargetFilter
{
    public string targetName;

    public List<ITargetable> Apply(List<ITargetable> input, EffectContext context)
    {
        var result = new List<ITargetable>();
        if (string.IsNullOrWhiteSpace(targetName)) return input;

        foreach (ITargetable t in input)
        {
            if (t is MinionInstance minion && minion.SourceCard.cardName == targetName)
                result.Add(t);
        }

        return result;
    }
}
[Serializable]
public class FirstTargetFilter : ITargetFilter
{
    public List<ITargetable> Apply(List<ITargetable> input, EffectContext context)
    {
        var result = new List<ITargetable>();
        if (input != null && input.Count > 0) result.Add(input[0]);
        return result;
    }
}
// [Serializable]
// public class OwnLane : ITargetLogic
// {
//     [SerializeReference] [SubclassSelector]
//     public List<ITargetFilter> filters = new List<ITargetFilter>();
//
//     [SerializeReference] [SubclassSelector]
//     public ITargetLogic fallback;
//
//     public List<ITargetable> GetTargets(EffectContext context)
//     {
//         var targets = new List<ITargetable>();
//
//         if (context.Instance is FieldableCardInstance fieldCtx && fieldCtx.Lane != null)
//         {
//             var portal = fieldCtx.Owner.playerSide == PlayerSide.Left
//                 ? fieldCtx.Lane.LeftPortal
//                 : fieldCtx.Lane.RightPortal;
//
//             for (int i = 0; i < portal.MinionCount; i++)
//             {
//                 var m = portal.GetMinion(i);
//                 if (m != null) targets.Add(m);
//             }
//         }
//
//         foreach (var f in filters)
//         {
//             if (f != null) targets = f.Apply(targets, context);
//         }
//
//         if ((targets == null || targets.Count == 0) && fallback != null)
//             return fallback.GetTargets(context);
//
//         return targets ?? new List<ITargetable>();
//     }
// }
