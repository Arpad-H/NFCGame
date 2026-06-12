using System.Collections.Generic;

// An event waiting in the board queue.
//
// The GameEvent envelope already carries Type, EffectSource (the card the event
// happened TO, e.g. the minion that was damaged) and the payload. PendingEvent
// adds only delivery scope: Target == null means broadcast to every living
// fielded card (the normal case); a non-null Target restricts delivery to that
// one instance, for events that genuinely concern a single card (e.g. OnPlayed).
//
// Note: interception events (OnAboutToTakeDamage / OnAboutToBeHealed) never go
// through the queue — their payloads are mutable (IsPrevented) and must resolve
// synchronously before the damage/heal is applied.
public readonly struct PendingEvent
{
    public readonly GameEvent Event;
    public readonly CardInstance Target;

    public PendingEvent(GameEvent gameEvent, CardInstance target = null)
    {
        Event = gameEvent;
        Target = target;
    }
}

// Holds the board's event queue state. Data only — the drain loop itself
// lives on Board (Step 3).
public class BoardEventQueue
{
    public readonly Queue<PendingEvent> Events = new();

    // Minions whose health hit 0 while draining. Collected during the drain,
    // processed as one batch afterward (deaths never recurse into the drain).
    public readonly List<MinionInstance> PendingDeaths = new();

    // True while the drain loop is running. Events raised mid-drain are
    // enqueued and picked up by the already-running loop instead of starting
    // a nested (recursive) drain.
    public bool IsDraining;
}
