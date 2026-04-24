public interface ITargetable 
{
    void TakeDamage(int amount);
}
public interface IGameEventReceiver 
{
    void HandleEvent(GameEvent evt);
}