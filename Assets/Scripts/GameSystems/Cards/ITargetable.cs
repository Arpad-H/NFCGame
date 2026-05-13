using System.Threading.Tasks;
using GameSystems;

public interface ITargetable 
{
    Task TakeDamage(DamageEventData damageEventData);
    Task ModifyStat(MinionStats  stat, int amount);
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