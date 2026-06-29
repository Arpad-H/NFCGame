using System.Threading.Tasks;
using GameSystems;

public interface ITargetable 
{
    Task TakeDamage(DamageEventData damageEventData);
    Task Heal (HealEventData healEventData);
    Task ModifyStat(MinionStats  stat, int amount);
    string ToString();
}
public interface IGameEventReceiver 
{
    Task HandleEvent(GameEvent evt);
}
public interface IAudioOnGameEventReceiver
{
    // Returns the length (seconds) of the longest clip played for this event.
    float HandleAudioOnEvent(GameEvent evt);
}
public interface IPlayerTargetable : ITargetable
{
     Task DrawCard(int amount);
     Task DiscardCard(int amount);
}