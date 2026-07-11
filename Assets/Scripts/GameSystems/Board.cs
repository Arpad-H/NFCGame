using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameSystems;
using UnityEngine;
using Random = UnityEngine.Random;

public partial class Board // combat resolution lives in BoardCombat.cs
{
    public Lane[] lanes = new Lane[3];

    // Continuous "while on field" effects. Reevaluated whenever board state
    // changes so auras cover late-played minions and dynamic amounts.
    public readonly AuraRegistry AuraRegistry = new();

    // Kept in sync by BoardEventDispatcher.RoundStart. Effects that create new
    // card instances mid-game (SpawnCardEffect) read it for SummonedOnRound.
    public int CurrentRound = 1;
    private Dictionary<ResonanceType, List<Portal>> resonanceMap = new Dictionary<ResonanceType, List<Portal>>();
    private int maxCardsPerPortal;
    public bool shufflePortals = false;

    // Ever-increasing stamp handed to each card as it is fielded, so "the last
    // card the opponent placed" is a simple max over the cards still on the board.
    private long placementCounter;

    public void SetUpBoard(int maxCards)
    {
        maxCardsPerPortal = maxCards;

        //initialize lanes
        for (int i = 0; i < lanes.Length; i++)
        {
            lanes[i] = new Lane(i);
        }

        //Find all portals and assign them to player sides
        Portal[] allPortals = GameObject.FindObjectsByType<Portal>(FindObjectsSortMode.None);
        List<Portal> leftPortals = new List<Portal>();
        List<Portal> rightPortals = new List<Portal>();

        foreach (var p in allPortals)
        {
            int index = p.laneIndex;

            if (p.ownerSide == PlayerSide.Left)
                lanes[index].LeftPortal = p;
            else
                lanes[index].RightPortal = p;
        }

        //Shuffle both lists
        if (shufflePortals)
        {
            Debug.LogWarning("Not implemented functionally yet");
            ShuffleList(leftPortals);
            ShuffleList(rightPortals);
        }


        //Assign portals to Lanes
        if (leftPortals.Count == 3 && rightPortals.Count == 3)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i].LeftPortal = leftPortals[i];
                lanes[i].RightPortal = rightPortals[i];
            }
        }

        foreach (var player in WebSocketServerBehaviour.Instance.ConnectedPlayers)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                if (player.id == 1)
                {
                    lanes[i].LeftPortal.SetResonanceType(player.resonances[i]);
                }
                else if (player.id == 2)
                {
                    lanes[i].RightPortal.SetResonanceType(player.resonances[i]);
                }
            }
        }

        BuildResonanceIndex();
    }

    private void BuildResonanceIndex()
    {
        resonanceMap.Clear();
        foreach (var lane in lanes)
        {
            IndexPortal(lane.LeftPortal);
            IndexPortal(lane.RightPortal);
        }
    }

    private void IndexPortal(Portal portal)
    {
        if (!resonanceMap.ContainsKey(portal.resonance.ResonanceType))
            resonanceMap[portal.resonance.ResonanceType] = new List<Portal>();

        resonanceMap[portal.resonance.ResonanceType].Add(portal);
    }

    public async Task<bool> PlaceCard(FieldableCardInstance cardInstance)
    {
        // Showdown: the last contested lane accepts ANY card regardless of
        // resonance, dropped into the player's own-side portal there.
        if (IsShowdown)
        {
            Lane showdownLane = GetLastActiveLane();
            if (showdownLane == null)
            {
                Debug.LogWarning("Showdown is active but no lane is still contested.");
                return false;
            }

            Portal showdownPortal = cardInstance.Owner.playerSide == PlayerSide.Left
                ? showdownLane.LeftPortal
                : showdownLane.RightPortal;
            return await TryPlaceInPortal(cardInstance, showdownPortal);
        }

        if (resonanceMap.TryGetValue(cardInstance.SourceCard.resonance, out List<Portal> matchingPortals))
        {
            foreach (var portal in matchingPortals) //TODO matching portls is only ever one, n skip loop
            {
                // Ensure the portal belongs to the player trying to place the card
                if (portal.ownerSide != cardInstance.Owner.playerSide) continue;

                // A decided lane is out of play — nothing new can be fielded there.
                if (GetLaneForPortal(portal)?.IsDecided == true)
                {
                    Debug.LogWarning(
                        $"Lane for {portal.resonance} portal is already decided. Cannot place card.");
                    return false;
                }

                return await TryPlaceInPortal(cardInstance, portal);
            }
        }

        Debug.LogWarning($"No matching {cardInstance.SourceCard.resonance} portal found for {cardInstance.Owner}");
        return false;
    }

    // Shared placement body: validates the item-needs-a-minion and capacity
    // rules, then fields the card into the given portal. Used by both the
    // normal resonance-matched path and the showdown redirect.
    private async Task<bool> TryPlaceInPortal(FieldableCardInstance cardInstance, Portal portal)
    {
        if (portal == null) return false;

        if (cardInstance is ItemInstance && portal.GetCardCount() == 0)
        {
            Debug.LogWarning(
                $"Cannot place item {cardInstance.SourceCard.cardName} in empty portal {portal.resonance}. Must be placed on top of a minion.");
            return false;
        }

        if (portal.GetCardCount() >= maxCardsPerPortal)
        {
            Debug.LogWarning($"Portal for {portal.resonance} is full. Cannot place card.");
            return false;
        }

        Lane lane = GetLaneForPortal(portal);
        cardInstance.SetSourcePortal(portal).SetTargetLane(lane);
        await portal.AddCard(cardInstance);
        cardInstance.PlacementSequence = ++placementCounter;
        AuraRegistry.Reevaluate(); // newly placed card may enter existing auras
        Debug.Log(
            $"Placed {cardInstance.SourceCard.cardName} in {portal.resonance} portal in Lane {lane?.LaneIndex} for {cardInstance.Owner}");
        return true;
    }

    // Sends a fielded card to its owner's discard pile WITHOUT the death path —
    // no OnKilled, so a discarded minion fires no deathrattle and an item is
    // simply removed. Before it leaves, the card's own effect fields are
    // deactivated so buffs it applied to other targets (RemoveModifierEffect /
    // UnregisterAuraEffect wired to OnEffectFieldIsDeActivated) are lifted; its
    // continuous auras are cleared by Portal.RemoveCard. See DiscardLastPlacedEffect.
    public async Task SendToDiscard(FieldableCardInstance card)
    {
        if (card == null) return;
        Portal portal = card.SourcePortal;

        // The discarded card's OWN deactivation cleanup, run while its fields are
        // still active (see FieldableCardInstance.DetachCardFromThis).
        await card.DetachCardFromThis();

        // A rune-supplying item activates the effect field of the card directly
        // beneath it; that neighbour must release those runes when the item goes.
        if (card is ItemInstance && portal != null)
        {
            FieldableCardInstance below = portal.GetCardDirectlyBelow(card);
            if (below != null) await below.DetachCardFromThis();
        }

        // File the card's identity into its owner's pile before it leaves.
        card.Owner?.AddToDiscardPile(card.SourceCard);

        // Off the board without a death: RemoveCard also unregisters this card's
        // auras and cascades to anything stacked on top of it.
        portal?.RemoveCard(card);

        AuraRegistry.Reevaluate();
    }

    // Resolves the owner's portal whose resonance matches this card, without
    // fielding it. Spells use this: they evaluate their effect from a lane but
    // are never placed into a portal's card stack.
    public Portal GetOwnerPortal(FieldableCardInstance cardInstance)
    {
        if (resonanceMap.TryGetValue(cardInstance.SourceCard.resonance, out List<Portal> matchingPortals))
        {
            foreach (var portal in matchingPortals)
            {
                if (portal.ownerSide == cardInstance.Owner.playerSide)
                    return portal;
            }
        }

        return null;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    public Lane GetLaneForPortal(Portal portal)
    {
        foreach (var lane in lanes)
        {
            if (lane.LeftPortal == portal || lane.RightPortal == portal)
                return lane;
        }

        return null;
    }

    // ── Lane outcomes (portal HP → 2/3 win → showdown) ───────────────────────
    //
    // Combat damages portals (BoardCombat.ResolveCombatTarget). After the combat
    // phase, GameManager asks the board which lanes were just decided, awards
    // them, clears them, and checks for a 2-of-3 win or a 1-1 showdown.

    public bool IsShowdown { get; private set; }

    // Awards any lane whose portal was just destroyed to the opposing side and
    // returns the lanes newly decided this call. At most one portal per lane can
    // be hit in a single combat (only one side ever faces an empty front there),
    // so there's never a tie to break.
    public List<Lane> ResolveDecidedLanes()
    {
        var newlyDecided = new List<Lane>();
        foreach (var lane in lanes)
        {
            if (lane.IsDecided) continue;

            if (lane.LeftPortal != null && lane.LeftPortal.IsDestroyed)
            {
                lane.WonBy = PlayerSide.Right;
                newlyDecided.Add(lane);
            }
            else if (lane.RightPortal != null && lane.RightPortal.IsDestroyed)
            {
                lane.WonBy = PlayerSide.Left;
                newlyDecided.Add(lane);
            }
        }

        return newlyDecided;
    }

    public int CountLanesWon(PlayerSide side)
    {
        int count = 0;
        foreach (var lane in lanes)
            if (lane.WonBy == side) count++;
        return count;
    }

    // The first still-contested lane. During showdown exactly one remains.
    public Lane GetLastActiveLane()
    {
        foreach (var lane in lanes)
            if (!lane.IsDecided) return lane;
        return null;
    }

    public void EnterShowdown()
    {
        IsShowdown = true;
    }

    // Empties both portals of a decided lane: every card is filed into its
    // owner's discard pile WITHOUT dying (no OnKilled/deathrattle), reusing
    // SendToDiscard. Cleared top-of-stack → bottom so the item cascade inside
    // Portal.RemoveCard never trips over an already-removed card.
    public async Task ClearLane(Lane lane)
    {
        if (lane == null) return;
        await ClearPortal(lane.LeftPortal);
        await ClearPortal(lane.RightPortal);
    }

    private async Task ClearPortal(Portal portal)
    {
        if (portal == null) return;
        var cards = portal.GetAllCardsInPortal(); // fresh list, safe to hold while mutating
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            await SendToDiscard(cards[i]);
        }
    }

    // ── Event queue ───────────────────────────────────────────────────────────
    //
    // All board events funnel through here and are drained in one place.
    // Events raised while a drain is running are enqueued and picked up by the
    // already-running loop, so delivery never recurses. Deaths are collected
    // during the drain and processed as a batch once the queue is empty.

    private readonly BoardEventQueue eventQueue = new();

    // Safety valve against event ping-pong loops (A triggers B triggers A...).
    private const int MaxEventsPerDrain = 1000;

    // How many of the most recent deliveries the runaway dump prints in order.
    // The cycle is almost always short, so the tail shows the whole loop; the
    // repeat-count summary that follows names the culprits over the full drain.
    private const int LoopDumpTailSize = 30;

    // Set true to log every delivered event as it resolves. Off by default —
    // a normal drain is dozens of events and this floods the console.
    public static bool VerboseEventLog;

    public Task HandleEventOnBoard(GameEvent gameEvent)
    {
        return RaiseEvent(gameEvent);
    }

    // Enqueue an event. target == null broadcasts to all living cards on the
    // board; a non-null target delivers to that single card. If no drain is
    // running, this call starts one and the returned Task completes when the
    // queue (and any resulting deaths) are fully processed. If a drain is
    // already running, the event is left for it and this returns immediately.
    public Task RaiseEvent(GameEvent gameEvent, CardInstance target = null)
    {
        eventQueue.Events.Enqueue(new PendingEvent(gameEvent, target));
        return eventQueue.IsDraining ? Task.CompletedTask : DrainEventQueue();
    }

    // Enqueue a reaction (OnDamaged/OnHealed): delivered before anything in
    // the main queue, so the response to a stat change is the next event out
    // once the currently-delivering event finishes its broadcast.
    public Task RaiseReaction(GameEvent gameEvent, CardInstance target = null)
    {
        eventQueue.Reactions.Enqueue(new PendingEvent(gameEvent, target));
        return eventQueue.IsDraining ? Task.CompletedTask : DrainEventQueue();
    }

    // Record a death for batch processing. The minion stays on the board (and
    // keeps receiving its own events, e.g. its OnKilled) until the batch runs.
    public void ReportDeath(MinionInstance minion, CardInstance killer)
    {
        foreach (var death in eventQueue.PendingDeaths)
        {
            if (death.Minion == minion) return;
        }

        eventQueue.PendingDeaths.Add(new DeathRecord(minion, killer));
    }

    private int deliveredThisDrain;

    // One line per delivery for the current drain, kept so a runaway loop can be
    // reconstructed after the fact. Cleared at the start of every drain, and
    // capped implicitly by MaxEventsPerDrain.
    private readonly List<string> drainTrace = new();
    private bool loopReported;

    private async Task DrainEventQueue()
    {
        eventQueue.IsDraining = true;
        deliveredThisDrain = 0;
        loopReported = false;
        drainTrace.Clear();
        try
        {
            while (eventQueue.Reactions.Count > 0 || eventQueue.Events.Count > 0 ||
                   eventQueue.PendingDeaths.Count > 0)
            {
                while (await DeliverNext())
                {
                }

                if (eventQueue.PendingDeaths.Count > 0)
                {
                    await ProcessDeathBatch();
                }
            }
        }
        finally
        {
            eventQueue.IsDraining = false;
        }
    }

    // Delivers the next pending event — reactions before the main queue.
    // Returns false when both queues are empty or the runaway guard trips.
    private async Task<bool> DeliverNext()
    {
        PendingEvent pending;
        bool isReaction = eventQueue.Reactions.Count > 0;
        if (isReaction)
        {
            pending = eventQueue.Reactions.Dequeue();
        }
        else if (eventQueue.Events.Count > 0)
        {
            pending = eventQueue.Events.Dequeue();
        }
        else
        {
            return false;
        }

        string description = DescribeDelivery(pending, isReaction);
        drainTrace.Add(description);
        if (VerboseEventLog) Debug.Log($"[EventQueue #{deliveredThisDrain + 1}] {description}");

        if (++deliveredThisDrain > MaxEventsPerDrain)
        {
            // Only the first trip carries the trace; the death batch can re-enter
            // DeliverNext afterwards and would otherwise re-dump an empty one.
            if (!loopReported) LogRunawayLoop();
            loopReported = true;
            eventQueue.Reactions.Clear();
            eventQueue.Events.Clear();
            drainTrace.Clear();
            return false;
        }

        await DeliverEvent(pending);

        // Board state may have shifted (status applied/removed, stats changed,
        // lane order changed) — keep continuous auras in sync. Reevaluate is
        // delta-based and side-effect free when nothing changed, so running it
        // per event is safe with current board sizes.
        AuraRegistry.Reevaluate();

        return true;
    }

    // "REACT OnDamaged@Rat#7(P1) (amount 1 from Thornback#3(P2)) → broadcast"
    private static string DescribeDelivery(PendingEvent pending, bool isReaction)
    {
        string kind = isReaction ? "REACT" : "EVENT";
        string scope = pending.Target != null ? GameEvent.Describe(pending.Target) : "broadcast";
        return $"{kind} {pending.Event} → {scope}";
    }

    // Dumps the tail of the drain in delivery order, then the events that repeated
    // most across the whole drain. Between them the offending cards (identified by
    // #InstanceId) and the trigger pair bouncing off each other are readable
    // straight off the console.
    private void LogRunawayLoop()
    {
        var counts = new Dictionary<string, int>();
        foreach (string entry in drainTrace)
        {
            counts.TryGetValue(entry, out int c);
            counts[entry] = c + 1;
        }

        var repeated = new List<KeyValuePair<string, int>>(counts);
        repeated.Sort((a, b) => b.Value.CompareTo(a.Value));

        var report = new System.Text.StringBuilder();
        report.AppendLine(
            $"Board event queue exceeded {MaxEventsPerDrain} events in one drain — trigger loop. Dropping remaining events.");
        report.AppendLine(
            $"Queued when it tripped: {eventQueue.Reactions.Count} reaction(s), {eventQueue.Events.Count} event(s), " +
            $"{eventQueue.PendingDeaths.Count} pending death(s).");

        report.AppendLine($"── last {Math.Min(LoopDumpTailSize, drainTrace.Count)} deliveries (oldest first) ──");
        for (int i = Math.Max(0, drainTrace.Count - LoopDumpTailSize); i < drainTrace.Count; i++)
        {
            report.AppendLine($"  {i + 1,4}: {drainTrace[i]}");
        }

        report.AppendLine("── most repeated this drain ──");
        for (int i = 0; i < Math.Min(5, repeated.Count); i++)
        {
            if (repeated[i].Value < 2) break;
            report.AppendLine($"  {repeated[i].Value,4}× {repeated[i].Key}");
        }

        Debug.LogError(report.ToString());
    }

    private async Task DeliverEvent(PendingEvent pending)
    {
        if (pending.Target != null)
        {
            if (pending.Target is IGameEventReceiver receiver && CanReceive(pending.Target, pending.Event))
            {
                await receiver.HandleEvent(pending.Event);
            }

            return;
        }

        // Broadcast to every fielded card — minions AND items/spells, so cards
        // like "round start: heal the holder" can carry their own triggers.
        FieldableCardInstance[] snapshot = GetAllCardsOnBoard().ToArray();
        foreach (FieldableCardInstance card in snapshot)
        {
            if (card is not IGameEventReceiver cardReceiver) continue;
            if (!CanReceive(card, pending.Event)) continue;

            await cardReceiver.HandleEvent(pending.Event);
        }
    }

    // Dead minions stop receiving broadcasts, but a dying minion still sees its
    // own events (OnDamaged/OnKilled deathrattles) while awaiting batch removal.
    private static bool CanReceive(CardInstance card, GameEvent gameEvent)
    {
        if (card is MinionInstance minion && !minion.IsAlive)
        {
            return ReferenceEquals(card, gameEvent.EffectSource);
        }

        return true;
    }

    // Called by ReviveEffect while OnKilled events are being delivered. The
    // minion's health must already be restored; the death batch will then skip
    // its removal instead of processing the corpse.
    public void MarkRevived(MinionInstance minion)
    {
        eventQueue.RevivedMinions.Add(minion);
    }

    // For each collected death: broadcast OnKilled, drain whatever those
    // triggers enqueue, then remove the corpses. New deaths caused by OnKilled
    // triggers land in PendingDeaths and are handled by the outer drain loop.
    private async Task ProcessDeathBatch()
    {
        List<DeathRecord> batch = new(eventQueue.PendingDeaths);
        eventQueue.PendingDeaths.Clear();

        foreach (var death in batch)
        {
            eventQueue.Events.Enqueue(new PendingEvent(
                new GameEvent(GameEventType.OnKilled, death.Minion, new SourceEventData(death.Killer))));
        }

        // Drain the OnKilled events (and any reactions they cause) before
        // removing the corpses, so deathrattles still see the dying minions.
        while (await DeliverNext())
        {
        }

        foreach (var death in batch)
        {
            // A ReviveEffect fired during the OnKilled drain: the minion stays
            // on the board with its auras and modifiers untouched.
            if (eventQueue.RevivedMinions.Remove(death.Minion)) continue;

            // Safety net: a dead aura source must never keep buffing the board,
            // even if its card SO has no explicit unregister trigger.
            AuraRegistry.UnregisterAllFrom(death.Minion);
            death.Minion.ProcessDeath();
        }

        AuraRegistry.Reevaluate();
    }

    public List<MinionInstance> GetAllMinionsOnBoard()
    {
        List<MinionInstance> minions = new List<MinionInstance>();

        foreach (Lane lane in lanes)
        {
            if (lane.LeftPortal != null)
            {
                minions.AddRange(lane.LeftPortal.GetAllMinionsInPortal());
            }

            if (lane.RightPortal != null)
            {
                minions.AddRange(lane.RightPortal.GetAllMinionsInPortal());
            }
        }
        return minions;
    }

    // Every fielded card including items and spells — the event broadcast set.
    public List<FieldableCardInstance> GetAllCardsOnBoard()
    {
        List<FieldableCardInstance> cards = new List<FieldableCardInstance>();

        foreach (Lane lane in lanes)
        {
            if (lane.LeftPortal != null) cards.AddRange(lane.LeftPortal.GetAllCardsInPortal());
            if (lane.RightPortal != null) cards.AddRange(lane.RightPortal.GetAllCardsInPortal());
        }

        return cards;
    }

    // Rotates one side's card stacks one lane down (0→1→2→0), visuals and all.
    // Holder/attachment relationships survive because whole stacks move
    // together. Cards may end up in a portal whose resonance differs from
    // their own — that mismatch is queryable game state, not an error.
    public void ShiftLanesDown(PlayerSide side)
    {
        var portals = new Portal[lanes.Length];
        for (int i = 0; i < lanes.Length; i++)
        {
            portals[i] = side == PlayerSide.Left ? lanes[i].LeftPortal : lanes[i].RightPortal;
        }

        var contents = new List<(FieldableCardInstance context, BoardTokenVisualizer visual)>[lanes.Length];
        for (int i = 0; i < portals.Length; i++)
        {
            contents[i] = portals[i].TakeAllCards();
        }

        for (int i = 0; i < portals.Length; i++)
        {
            int targetIndex = (i + 1) % portals.Length;
            portals[targetIndex].ReceiveCards(contents[i], lanes[targetIndex]);
        }

        AuraRegistry.Reevaluate(); // positional auras (MinionInFront etc.) may have new targets
    }
}


public class Lane
{
    public int LaneIndex; // 0,1,2. 0 is Top, 1 is middle, 2 is bottom
    public Portal LeftPortal;
    public Portal RightPortal;

    // Set once one side's portal in this lane is destroyed: the OTHER side has
    // won the lane. A decided lane is inactive — it no longer fights and rejects
    // new plays. null = still contested.
    public PlayerSide? WonBy;
    public bool IsDecided => WonBy.HasValue;

    public Lane(int index)
    {
        LaneIndex = index;
    }
}