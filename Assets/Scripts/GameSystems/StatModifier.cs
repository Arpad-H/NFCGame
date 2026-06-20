// A sourced, reversible stat change on a minion. Source identifies who granted
// it (an aura's ActiveAura, a StatusEffectInstance, a card instance, or null
// for permanent buffs) so it can be found and removed when the source goes away.
public class StatModifier
{
    public object Source;
    public int Health;
    public int Attack;

    public StatModifier(object source, int health, int attack)
    {
        Source = source;
        Health = health;
        Attack = attack;
    }
}
