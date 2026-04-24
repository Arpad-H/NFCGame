public enum GameEventType
{
    OnPlayed,
    OnRoundStart,
    OnRoundEnd,
    OnAttack,
    OnDamaged,
    OnKilled
}

public readonly struct GameEvent
{
    public readonly GameEventType Type;
    public readonly FieldableCardContext Context;

    public GameEvent(GameEventType type, FieldableCardContext context)
    {
        Type = type;
        Context = context;
    }
}
