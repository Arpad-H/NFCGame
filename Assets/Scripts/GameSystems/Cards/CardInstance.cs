using System;

//instance of a card type like a minion on the board or a spell 
public sealed class MinionInstance : ITargetable, IGameEventReceiver
{
    public CardData SourceCard { get; }
    public MinionType Definition { get; }

    public int CurrentHealth { get; private set; }
    public int CurrentAttack { get; private set; }

    public event Action<int> OnHealthChanged;
    public event Action OnDeath;

    public MinionInstance(CardData sourceCard, MinionType definition)
    {
        SourceCard = sourceCard;
        Definition = definition;
        CurrentHealth = definition.baseHealth;
        CurrentAttack = definition.baseAttack;
    }

    public void TakeDamage(DamageEventData damageEventData)
    {
        CurrentHealth -= damageEventData.Amount;
        OnHealthChanged?.Invoke(CurrentHealth);
        if (CurrentHealth <= 0)
        {
            OnDeath?.Invoke();
        }

     //   HandleEvent(new GameEvent(GameEventType.OnDamaged, this, damageEventData.Source));
    }

    public void HandleEvent(GameEvent evt)
    {
        foreach (var effect in Definition.effects)
        {
            if (effect is ITriggeredEffect triggered && triggered.CanTrigger(evt.Type))
                effect.Execute(evt.Context);
        }
    }
}