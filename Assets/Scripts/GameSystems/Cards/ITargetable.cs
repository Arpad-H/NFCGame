using System.Threading.Tasks;

public interface ITargetable 
{
    Task TakeDamage(DamageEventData damageEventData);
}
public interface IGameEventReceiver 
{
    Task HandleEvent(GameEvent evt);
}
public interface IPlayerTargetable : ITargetable
{
     Task DrawCard(int amount);
    Task DiscardCard(int amount);
}//TODO can it be done with handle event system instead?