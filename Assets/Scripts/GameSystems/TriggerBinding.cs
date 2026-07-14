using GameSystems;

// One binding per trigger per card INSTANCE, built once at Initialize().
// Triggers themselves live on shared ScriptableObjects and must stay stateless;
// any per-instance state a trigger needs goes in StateSlot.
public class TriggerBinding
{
    public TriggerBinding() { }

    public TriggerBinding(IEventTrigger trigger, EffectFieldPosition effectIndex)
    {
        Trigger = trigger;
        EffectIndex = effectIndex;
    }

    public IEventTrigger Trigger { get; set; }

    // The effect this binding fires. Currently triggers execute their own
    // serialized effect internally; this slot is filled once triggers expose it.
    public ICardEffect Effect { get; set; }

    // Which effect field (Passive/Effect1/Effect2/Combat) this binding belongs to.
    // Drives IsActive when rune fields activate/deactivate.
    public EffectFieldPosition EffectIndex { get; set; }

    public bool IsActive { get; set; } = true;

    // Per-instance trigger state (e.g. "has fired", rounds counted).
    // Owned by the binding so shared trigger SOs never carry instance state.
    public object StateSlot { get; set; }

    // The card these triggers are bound to (the status-effect host for status
    // bindings). Stamped lazily by the dispatch loops, so CanTrigger
    // implementations can self-check BEFORE consuming per-instance state
    // (see TriggerNTimes.onlySelf).
    public CardInstance Owner { get; set; }
}
