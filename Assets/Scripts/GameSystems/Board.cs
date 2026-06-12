using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameSystems;
using UnityEngine;
using Random = UnityEngine.Random;

public class Board
{
    public Lane[] lanes = new Lane[3];
    private Dictionary<ResonanceType, List<Portal>> resonanceMap = new Dictionary<ResonanceType, List<Portal>>();
    private int maxCardsPerPortal;
    public bool shufflePortals = false;

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
        if (resonanceMap.TryGetValue(cardInstance.SourceCard.resonance, out List<Portal> matchingPortals))
        {
            foreach (var portal in matchingPortals) //TODO matching portls is only ever one, n skip loop
            {
                if (cardInstance is ItemInstance && portal.GetCardCount() == 0)
                {
                    Debug.LogWarning(
                        $"Cannot place item {cardInstance.SourceCard.cardName} in empty portal {portal.resonance}. Must be placed on top of a minion.");
                    return false;
                }

                // Ensure the portal belongs to the player trying to place the card
                if (portal.ownerSide == cardInstance.Owner.playerSide)
                {
                    if (portal.GetCardCount() >= maxCardsPerPortal)
                    {
                        Debug.LogWarning($"Portal for {portal.resonance} is full. Cannot place card.");
                        return false;
                    }

                    cardInstance.SetSourcePortal(portal).SetTargetLane(GetLaneForPortal(portal));
                    await portal.AddCard(cardInstance);
                    Debug.Log(
                        $"Placed {cardInstance.SourceCard.cardName} in {portal.resonance} portal in Lane {GetLaneForPortal(portal).LaneIndex} for {cardInstance.Owner}");
                    return true;
                }
            }
        }

        Debug.LogWarning($"No matching {cardInstance.SourceCard.resonance} portal found for {cardInstance.Owner}");
        return false;
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

    // ── Event queue ───────────────────────────────────────────────────────────
    //
    // All board events funnel through here and are drained in one place.
    // Events raised while a drain is running are enqueued and picked up by the
    // already-running loop, so delivery never recurses. Deaths are collected
    // during the drain and processed as a batch once the queue is empty.

    private readonly BoardEventQueue eventQueue = new();

    // Safety valve against event ping-pong loops (A triggers B triggers A...).
    private const int MaxEventsPerDrain = 1000;

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

    private async Task DrainEventQueue()
    {
        eventQueue.IsDraining = true;
        deliveredThisDrain = 0;
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
        if (eventQueue.Reactions.Count > 0)
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

        if (++deliveredThisDrain > MaxEventsPerDrain)
        {
            Debug.LogError(
                $"Board event queue exceeded {MaxEventsPerDrain} events in one drain — possible trigger loop. Dropping remaining events.");
            eventQueue.Reactions.Clear();
            eventQueue.Events.Clear();
            return false;
        }

        await DeliverEvent(pending);
        return true;
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

        FieldableCardInstance[] snapshot = GetAllMinionsOnBoard().ToArray();
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
            death.Minion.ProcessDeath();
        }
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
}


public class Lane
{
    public int LaneIndex; // 0,1,2. 0 is Top, 1 is middle, 2 is bottom
    public Portal LeftPortal;
    public Portal RightPortal;

    public Lane(int index)
    {
        LaneIndex = index;
    }
}