using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using GameSystems;
using UnityEngine;

// Lane-by-lane combat resolution: lanes resolve top → mid → bottom. Within a
// lane, two front minions that target each other clash SIMULTANEOUSLY — they
// meet in the middle, both hits use pre-impact attack values, and both land
// even if one (or both) dies to the other's blow. Non-mutual lanes (stunned/
// sleeping/stealthed fronts, empty portals, face hits) fall back to sequential
// one-sided attacks, the active player's side swinging first.
//
// This replaces the old per-card DefaultCombatBehaviour attack, whose broadcast
// order made combat one-sided: a minion killed early never hit back. A card's
// own OnCombatResolution trigger (Ghostrider's cleave) now fires at the instant
// that minion's blow lands, so splash reaches the neighbouring lanes mid-clash
// rather than after the whole board has fought.
public partial class Board
{
    // Each clash partner stops just short of the midpoint so the two
    // visualizers meet instead of overlapping.
    private const float ClashMeetFraction = 0.45f;
    private const float LungeFraction = 0.6f; // one-sided attacks keep the old lunge distance
    private const float MoveInDuration = 0.2f;
    private const float MoveOutDuration = 0.3f;

    // activeSide is the player whose turn just ended. Their minions get
    // priority wherever combat can't be simultaneous: they swing first in a
    // one-sided lane, and their hit lands first inside a clash (which only
    // affects the order of reactions, never whether both blows connect).
    public async Task ResolveCombat(PlayerSide activeSide)
    {
        // Combat runs with the queue held in draining mode so OnDamaged
        // reactions and deaths queue up behind the whole phase — the same
        // semantics the old single-broadcast combat had. The drain below then
        // delivers those reactions and finally the death batch (which is why a
        // clash corpse can still tween back to its slot before being removed).
        eventQueue.IsDraining = true;
        try
        {
            foreach (var lane in lanes)
            {
                await ResolveLaneCombat(lane, activeSide);
            }
        }
        finally
        {
            eventQueue.IsDraining = false;
        }

        // Flush the OnDamaged reactions and deaths that combat deferred.
        await DrainEventQueue();
    }

    // A minion's own combat extras (Ghostrider's cleave) fire at the instant its
    // blow lands, so splash damage happens while the two are locked together in
    // the middle rather than after the whole board has finished fighting.
    // Delivered straight to the attacker: only a minion that actually swings
    // gets to act, and the deferred death batch means a corpse never does.
    private static Task RaiseCombatBehaviour(MinionInstance attacker)
    {
        return attacker.HandleEvent(new GameEvent(GameEventType.OnCombatResolution, attacker));
    }

    private async Task ResolveLaneCombat(Lane lane, PlayerSide activeSide)
    {
        var leftFront = GetFrontMinion(lane.LeftPortal);
        var rightFront = GetFrontMinion(lane.RightPortal);

        bool leftActs = CanTakeCombatAction(leftFront);
        bool rightActs = CanTakeCombatAction(rightFront);

        var leftTarget = leftActs ? ResolveCombatTarget(leftFront, lane) : null;
        var rightTarget = rightActs ? ResolveCombatTarget(rightFront, lane) : null;

        bool activeIsLeft = activeSide == PlayerSide.Left;

        // Mutual clash: both fronts act and target each other.
        if (leftActs && rightActs &&
            ReferenceEquals(leftTarget, rightFront) && ReferenceEquals(rightTarget, leftFront))
        {
            await ResolveClash(activeIsLeft ? leftFront : rightFront, activeIsLeft ? rightFront : leftFront);
            return;
        }

        var first = activeIsLeft ? leftFront : rightFront;
        var second = activeIsLeft ? rightFront : leftFront;
        bool firstActs = activeIsLeft ? leftActs : rightActs;
        bool secondActs = activeIsLeft ? rightActs : leftActs;
        var firstTarget = activeIsLeft ? leftTarget : rightTarget;

        if (firstActs) await ResolveSingleAttack(first, firstTarget);

        // Re-check: the first attack may have killed the other front (e.g. a
        // stealthed attacker its victim couldn't target back), and the
        // survivor's target may have changed (a stealthed minion woken by the
        // hit is now targetable).
        if (secondActs && second.IsAlive)
        {
            await ResolveSingleAttack(second, ResolveCombatTarget(second, lane));
        }
    }

    // Both minions meet between their slots, exchange damage snapshot at the
    // moment of impact, then both return — corpses included ("return, then
    // die"; removal happens in the death batch after the combat phase).
    // The active player's minion is passed first and lands its blow first.
    private async Task ResolveClash(MinionInstance first, MinionInstance second)
    {
        int firstDamage = first.CurrentAttack;
        int secondDamage = second.CurrentAttack;

        var firstVis = first.SourcePortal?.GetVisualizer(first);
        var secondVis = second.SourcePortal?.GetVisualizer(second);
        bool animated = firstVis != null && secondVis != null;

        Vector3 firstHome = default, secondHome = default;
        if (animated)
        {
            firstHome = firstVis.transform.position;
            secondHome = secondVis.transform.position;
            await Task.WhenAll(
                firstVis.transform.DOMove(Vector3.Lerp(firstHome, secondHome, ClashMeetFraction), MoveInDuration)
                    .SetEase(Ease.InCubic).AwaitSafe(),
                secondVis.transform.DOMove(Vector3.Lerp(secondHome, firstHome, ClashMeetFraction), MoveInDuration)
                    .SetEase(Ease.InCubic).AwaitSafe());
        }

        // One collision, one impact cue — both blows land inside it.
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMinionClashSound();

        await second.TakeDamage(
            new DamageEventData(firstDamage, first, DamageSourceType.Attack) { IsClashHit = true });
        await first.TakeDamage(
            new DamageEventData(secondDamage, second, DamageSourceType.Attack) { IsClashHit = true });
        Debug.Log(
            $"Clash in lane {first.Lane?.LaneIndex}: {first.SourceCard.cardName} ({firstDamage} dmg) <-> {second.SourceCard.cardName} ({secondDamage} dmg)");

        // A clash is two simultaneous blows, so it produces two directional
        // tiles. Both damages have landed above, so each side's IsAlive reflects
        // whether its own blow was lethal.
        GameHistory.Record(new HistoryEntry(HistoryKind.Attack, HistoryActor.FromCard(first),
            new[] { HistoryActor.FromCard(second) }, firstDamage, !second.IsAlive));
        GameHistory.Record(new HistoryEntry(HistoryKind.Attack, HistoryActor.FromCard(second),
            new[] { HistoryActor.FromCard(first) }, secondDamage, !first.IsAlive));

        // Both are still locked together in the middle: this is where a cleave
        // splashes the neighbouring lanes. Fires even for a minion that just
        // died to the other's blow — its attack landed simultaneously.
        await RaiseCombatBehaviour(first);
        await RaiseCombatBehaviour(second);

        if (animated)
        {
            await Task.WhenAll(
                firstVis.transform.DOMove(firstHome, MoveOutDuration).SetEase(Ease.OutCubic).AwaitSafe(),
                secondVis.transform.DOMove(secondHome, MoveOutDuration).SetEase(Ease.OutCubic).AwaitSafe());
        }

        await first.HandleEvent(new GameEvent(GameEventType.OnAttack, first,
            new AttackEventData(new List<ITargetable> { second })));
        await second.HandleEvent(new GameEvent(GameEventType.OnAttack, second,
            new AttackEventData(new List<ITargetable> { first })));
    }

    // The old DefaultAttackEffect behaviour: lunge toward the target, hit,
    // return. Used for every non-mutual combat action (face hits included).
    private async Task ResolveSingleAttack(MinionInstance attacker, ITargetable target)
    {
        int amount = attacker.CurrentAttack;
        var visualizer = attacker.SourcePortal?.GetVisualizer(attacker);
        Vector3 originalPos = default;

        if (visualizer != null)
        {
            originalPos = visualizer.transform.position;
            Vector3 targetPos = GetImpactPosition(target, originalPos);
            await visualizer.transform.DOMove(Vector3.Lerp(originalPos, targetPos, LungeFraction), MoveInDuration)
                .SetEase(Ease.InCubic).AwaitSafe();
        }

        await target.TakeDamage(new DamageEventData(amount, attacker, DamageSourceType.Attack));

        // Record the blow for the history bar. TakeDamage applies health
        // synchronously, so a minion target's IsAlive already reflects the kill.
        bool lethal = target is MinionInstance killed && !killed.IsAlive;
        GameHistory.Record(new HistoryEntry(HistoryKind.Attack, HistoryActor.FromCard(attacker),
            new[] { HistoryActor.FromTarget(target) }, amount, lethal));

        await RaiseCombatBehaviour(attacker); // cleave etc. splashes at the point of impact

        if (visualizer != null)
            await visualizer.transform.DOMove(originalPos, MoveOutDuration).SetEase(Ease.OutCubic).AwaitSafe();

        Debug.Log($"[Combat] {attacker} → hits {target} for {amount}");

        await attacker.HandleEvent(new GameEvent(GameEventType.OnAttack, attacker,
            new AttackEventData(new List<ITargetable> { target })));
    }

    // Deaths are batched until combat ends, so a minion killed by an earlier
    // lane's splash is still sitting in its portal. It neither swings nor
    // shields the minion behind it — the corpse is on its way off the board.
    private static MinionInstance GetFrontMinion(Portal portal)
    {
        if (portal == null) return null;
        foreach (var minion in portal.GetAllMinionsInPortal())
        {
            if (minion.IsAlive) return minion;
        }

        return null;
    }

    private static bool CanTakeCombatAction(MinionInstance minion)
    {
        if (minion == null || !minion.IsAlive) return false;

        if (minion.HasStatusEffect(StatusEffectType.Stun) || minion.HasStatusEffect(StatusEffectType.Sleep))
        {
            Debug.Log($"{minion.SourceCard.cardName} is stunned/asleep and skips its attack.");
            return false;
        }

        return true;
    }

    // Mirrors the Default target logic: the frontmost enemy minion that is
    // neither dead nor stealthed, otherwise the enemy player.
    private static ITargetable ResolveCombatTarget(MinionInstance attacker, Lane lane)
    {
        var enemyPortal = attacker.Opponent.playerSide == PlayerSide.Left ? lane.LeftPortal : lane.RightPortal;
        if (enemyPortal != null)
        {
            foreach (var minion in enemyPortal.GetAllMinionsInPortal())
            {
                if (!minion.IsAlive) continue;
                if (minion.HasStatusEffect(StatusEffectType.Stealth)) continue;
                return minion;
            }
        }

        return attacker.Opponent;
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
