public enum GameEventType
{
    OnPlayed,
    OnRoundStart,
    OnRoundEnd,
    OnAttack,
    OnDamaged,
    OnKilled,
    OnCombatResolution,
    OnCardDrawn,
    OnCardDiscarded
}

public readonly struct DamageEventData
{
    public readonly int Amount;
    public readonly CardInstance Source;

    public DamageEventData(int amount, CardInstance source = null)
    {
        Amount = amount;
        Source = source;
    }
}

public readonly struct GameEvent
{
    public readonly GameEventType Type;
    public readonly FieldableCardInstance Instance; // Card which this event triggers from.
    public readonly object GameEventPayload; // optional extra data

    public GameEvent(GameEventType type, FieldableCardInstance instance, object gameEventPayload = null)
    {
        Type = type;
        Instance = instance;
        GameEventPayload = gameEventPayload;
    }
}