using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using GameSystems;
using UnityEngine;

// Lane-by-lane combat resolution: lanes resolve top → mid → bottom. Within a
// lane, two front minions that target each other clash SIMULTANEOUSLY — they
// meet in the middle, both hits use pre-impact attack values, and both land
// even if one (or both) dies to the other's blow. Non-mutual lanes (stunned/
// sleeping/stealthed fronts, empty portals, face hits) fall back to
// sequential one-sided attacks, left side first.
//
// This replaces the old per-card DefaultCombatBehaviour attack, whose
// broadcast order made combat one-sided: a minion killed early never hit
// back. Cards with their own OnCombatResolution triggers (e.g. Ghostrider's
// cleave) still fire — the event is broadcast after the default clashes.
public partial class Board
{
    // Each clash partner stops just short of the midpoint so the two
    // visualizers meet instead of overlapping.
    private const float ClashMeetFraction = 0.45f;
    private const float LungeFraction = 0.6f; // one-sided attacks keep the old lunge distance
    private const float MoveInDuration = 0.2f;
    private const float MoveOutDuration = 0.3f;

    public async Task ResolveCombat()
    {
        // Combat runs with the queue held in draining mode so OnDamaged
        // reactions and deaths queue up behind the whole phase — the same
        // semantics the old single-broadcast combat had. The drain below then
        // delivers reactions, card-specific OnCombatResolution triggers and
        // finally the death batch (which is why a clash corpse can still tween
        // back to its slot before being removed).
        eventQueue.IsDraining = true;
        try
        {
            foreach (var lane in lanes)
            {
                await ResolveLaneCombat(lane);
            }
        }
        finally
        {
            eventQueue.IsDraining = false;
        }

        eventQueue.Events.Enqueue(new PendingEvent(new GameEvent(GameEventType.OnCombatResolution, null)));
        await DrainEventQueue();
    }

    private async Task ResolveLaneCombat(Lane lane)
    {
        var leftFront = GetFrontMinion(lane.LeftPortal);
        var rightFront = GetFrontMinion(lane.RightPortal);

        bool leftActs = CanTakeCombatAction(leftFront);
        bool rightActs = CanTakeCombatAction(rightFront);

        var leftTarget = leftActs ? ResolveCombatTarget(leftFront, lane) : null;
        var rightTarget = rightActs ? ResolveCombatTarget(rightFront, lane) : null;

        // Mutual clash: both fronts act and target each other.
        if (leftActs && rightActs &&
            ReferenceEquals(leftTarget, rightFront) && ReferenceEquals(rightTarget, leftFront))
        {
            await ResolveClash(leftFront, rightFront);
            return;
        }

        if (leftActs) await ResolveSingleAttack(leftFront, leftTarget);

        // Re-check: the left attack may have killed the right front (e.g. a
        // stealthed left minion the right side couldn't target back), and its
        // target may have changed (a stealthed minion woken by the hit).
        if (rightActs && rightFront.IsAlive)
        {
            await ResolveSingleAttack(rightFront, ResolveCombatTarget(rightFront, lane));
        }
    }

    // Both minions meet between their slots, exchange damage snapshot at the
    // moment of impact, then both return — corpses included ("return, then
    // die"; removal happens in the death batch after the combat phase).
    private async Task ResolveClash(MinionInstance left, MinionInstance right)
    {
        int leftDamage = left.CurrentAttack;
        int rightDamage = right.CurrentAttack;

        var leftVis = left.SourcePortal?.GetVisualizer(left);
        var rightVis = right.SourcePortal?.GetVisualizer(right);
        bool animated = leftVis != null && rightVis != null;

        Vector3 leftHome = default, rightHome = default;
        if (animated)
        {
            leftHome = leftVis.transform.position;
            rightHome = rightVis.transform.position;
            await Task.WhenAll(
                leftVis.transform.DOMove(Vector3.Lerp(leftHome, rightHome, ClashMeetFraction), MoveInDuration)
                    .SetEase(Ease.InCubic).AwaitSafe(),
                rightVis.transform.DOMove(Vector3.Lerp(rightHome, leftHome, ClashMeetFraction), MoveInDuration)
                    .SetEase(Ease.InCubic).AwaitSafe());
        }

        await left.TakeDamage(new DamageEventData(rightDamage, right, DamageSourceType.Attack));
        await right.TakeDamage(new DamageEventData(leftDamage, left, DamageSourceType.Attack));
        Debug.Log(
            $"Clash in lane {left.Lane?.LaneIndex}: {left.SourceCard.cardName} ({leftDamage} dmg) <-> {right.SourceCard.cardName} ({rightDamage} dmg)");

        if (animated)
        {
            await Task.WhenAll(
                leftVis.transform.DOMove(leftHome, MoveOutDuration).SetEase(Ease.OutCubic).AwaitSafe(),
                rightVis.transform.DOMove(rightHome, MoveOutDuration).SetEase(Ease.OutCubic).AwaitSafe());
        }

        await left.HandleEvent(new GameEvent(GameEventType.OnAttack, left,
            new AttackEventData(new List<ITargetable> { right })));
        await right.HandleEvent(new GameEvent(GameEventType.OnAttack, right,
            new AttackEventData(new List<ITargetable> { left })));
    }

    // The old DefaultAttackEffect behaviour: lunge toward the target, hit,
    // return. Used for every non-mutual combat action (face hits included).
    private async Task ResolveSingleAttack(MinionInstance attacker, ITargetable target)
    {
        int amount = attacker.CurrentAttack;
        var visualizer = attacker.SourcePortal?.GetVisualizer(attacker);

        if (visualizer != null)
        {
            Vector3 originalPos = visualizer.transform.position;
            Vector3 targetPos = GetImpactPosition(target, originalPos);
            await visualizer.transform.DOMove(Vector3.Lerp(originalPos, targetPos, LungeFraction), MoveInDuration)
                .SetEase(Ease.InCubic).AwaitSafe();
            await target.TakeDamage(new DamageEventData(amount, attacker, DamageSourceType.Attack));
            await visualizer.transform.DOMove(originalPos, MoveOutDuration).SetEase(Ease.OutCubic).AwaitSafe();
        }
        else
        {
            await target.TakeDamage(new DamageEventData(amount, attacker, DamageSourceType.Attack));
        }

        Debug.Log($"attacker: {attacker.SourceCard.cardName}, target: {target}, damage: {amount}");

        await attacker.HandleEvent(new GameEvent(GameEventType.OnAttack, attacker,
            new AttackEventData(new List<ITargetable> { target })));
    }

    private static MinionInstance GetFrontMinion(Portal portal)
    {
        if (portal == null) return null;
        var minions = portal.GetAllMinionsInPortal();
        return minions.Count > 0 ? minions[0] : null;
    }

    private static bool CanTakeCombatAction(MinionInstance minion)
    {
        if (minion == null || !minion.IsAlive) return false;

        // No serialized DefaultCombatBehaviour (e.g. Ghostrider) means no
        // default attack — that card's combat comes from its own triggers.
        if (!minion.HasDefaultCombatBehaviour) return false;

        if (minion.HasStatusEffect(StatusEffectType.Stun) || minion.HasStatusEffect(StatusEffectType.Sleep))
        {
            Debug.Log($"{minion.SourceCard.cardName} is stunned/asleep and skips its attack.");
            return false;
        }

        return true;
    }

    // Mirrors the Default target logic: first non-stealthed enemy minion in
    // the lane, otherwise the enemy player.
    private static ITargetable ResolveCombatTarget(MinionInstance attacker, Lane lane)
    {
        var enemyPortal = attacker.Opponent.playerSide == PlayerSide.Left ? lane.LeftPortal : lane.RightPortal;
        ITargetable target = enemyPortal?.GetFirstTargetableMinion();
        return target ?? (ITargetable)attacker.Opponent;
    }

    private static Vector3 GetImpactPosition(ITargetable target, Vector3 fallback)
    {
        switch (target)
        {
            case MinionInstance minion when minion.SourcePortal != null:
                var visualizer = minion.SourcePortal.GetVisualizer(minion);
                return visualizer != null ? visualizer.transform.position : fallback;
            case Player player:
                return player.healthText.transform.position; // rough proxy
            default:
                return fallback;
        }
    }
}
