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

// A death collected during a drain. Killer is carried separately because the
// OnKilled event (with SourceEventData(Killer)) is only built when the death
// batch is processed, after the event queue has drained.
public readonly struct DeathRecord
{
    public readonly MinionInstance Minion;
    public readonly CardInstance Killer;

    public DeathRecord(MinionInstance minion, CardInstance killer)
    {
        Minion = minion;
        Killer = killer;
    }
}

// Holds the board's event queue state. Data only — the drain loop itself
// lives on Board.
public class BoardEventQueue
{
    public readonly Queue<PendingEvent> Events = new();

    // Reactions (OnDamaged/OnHealed) jump ahead of everything in Events but
    // stay FIFO among themselves, so an AoE hitting A then B resolves A's
    // reaction before B's. The drain always empties this queue first.
    public readonly Queue<PendingEvent> Reactions = new();

    // Minions whose health hit 0 while draining. Collected during the drain,
    // processed as one batch afterward (deaths never recurse into the drain).
    public readonly List<DeathRecord> PendingDeaths = new();

    // Minions revived while their OnKilled events were being delivered
    // (ReviveEffect). The death batch skips these instead of removing them.
    public readonly HashSet<MinionInstance> RevivedMinions = new();

    // True while the drain loop is running. Events raised mid-drain are
    // enqueued and picked up by the already-running loop instead of starting
    // a nested (recursive) drain.
    public bool IsDraining;
}
