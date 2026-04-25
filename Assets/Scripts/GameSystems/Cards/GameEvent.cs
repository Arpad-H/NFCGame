public enum GameEventType
{
    OnPlayed,
    OnRoundStart,
    OnRoundEnd,
    OnAttack,
    OnDamaged,
    OnKilled
}
public readonly struct DamageEventData
{
    public readonly int Amount;
    public readonly object Source;

    public DamageEventData(int amount, object source = null)
    {
        Amount = amount;
        Source = source;
    }
}

public readonly struct GameEvent
{
    public readonly GameEventType Type;
    public readonly FieldableCardContext Context; // Card which this event triggers from.
    public readonly object Payload;        // optional extra data
    
    public GameEvent(GameEventType type, FieldableCardContext context, object payload = null)
    {
        Type = type;
        Context = context;
        Payload = payload;
    }
}
