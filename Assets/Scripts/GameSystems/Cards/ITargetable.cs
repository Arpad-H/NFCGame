
public interface ITargetable 
{
    void TakeDamage(DamageEventData damageEventData);
}
public interface IGameEventReceiver 
{
    void HandleEvent(GameEvent evt);
}
public interface IPlayerTargetable : ITargetable
{
    void DrawCard(int amount);
    void DiscardCard(int amount);
}//TODO can it be done with handle event system instead?