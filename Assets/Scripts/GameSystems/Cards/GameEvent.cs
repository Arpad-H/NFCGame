public enum GameEventType
{
    OnPlayed,
    OnRoundStart,
    OnRoundEnd,
    OnAboutToAttack,
    OnAttack,
    OnAboutToTakeDamage,
    OnDamaged,
    OnKilled,
    OnCombatResolution,
    OnCardDrawn,
    OnCardDiscarded,
    ActivateEffectEvent,
    DeactivateEffectEvent,
}

public  class DamageEventData
{
    public  int Amount;
    public  CardInstance Source;
    public bool IsPrevented;

    public DamageEventData(int amount, CardInstance source = null)
    {
        Amount = amount;
        Source = source;
        IsPrevented = false;
    }
}

public readonly struct GameEvent
{
    public readonly GameEventType Type;
    public readonly FieldableCardInstance EffectSource; // Card which this event triggers from.
    public readonly object GameEventPayload; // optional extra data

    public GameEvent(GameEventType type, FieldableCardInstance instance, object gameEventPayload = null)
    {
        Type = type;
        EffectSource = instance;
        GameEventPayload = gameEventPayload;
    }
}