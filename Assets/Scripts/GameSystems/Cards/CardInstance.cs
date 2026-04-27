using System;

//instance of a card type like a minion on the board or a spell 
public abstract class CardInstance
{
    public Player Owner;
    public Player Opponent;
    public Board Board;
}

public abstract class CardInstance<T> : CardInstance where T : CardInstance<T>
{
    public T SetOwner(Player owner)
    {
        Owner = owner;
        return (T)this;
    }

    public T SetOpponent(Player opponent)
    {
        Opponent = opponent;
        return (T)this;
    }

    public T SetBoard(Board board)
    {
        Board = board;
        return (T)this;
    }
}

public class FieldableCardInstance : CardInstance<FieldableCardInstance>
{
    public Lane Lane;
    public Portal SourcePortal;
    public CardData SourceCard;
    public int SummonedOnRound;
    public FieldableCardInstance SetTargetLane(Lane lane)
    {
        Lane = lane;
        return this;
    }

    public FieldableCardInstance SetSourceCard(CardData card)
    {
        SourceCard = card;
        return this;
    }

    public FieldableCardInstance SetSourcePortal(Portal portal)
    {
        SourcePortal = portal;
        return this;
    }
    public FieldableCardInstance SetSummonedOnRound(int round)
    {
        SummonedOnRound = round;
        return this;
    }
    public virtual void Initialize(){}
} 

public class MinionInstance : FieldableCardInstance, ITargetable, IGameEventReceiver
{
    public MinionType Definition;
    public int CurrentHealth { get; private set; }
    public int CurrentAttack { get; private set; }

    public event Action<int> OnHealthChanged;
    public event Action OnDeath;

    public void TakeDamage(DamageEventData damageEventData)
    {
        CurrentHealth -= damageEventData.Amount;
        OnHealthChanged?.Invoke(CurrentHealth);
        HandleEvent(new GameEvent(GameEventType.OnDamaged, this, damageEventData.Source));
        if (CurrentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    public void HandleEvent(GameEvent evt)
    {
        MinionType minionType = SourceCard.cardType as MinionType;
        if (minionType != null)
            foreach (IEventTrigger effect in minionType.effects)
            {
                if (effect != null && effect.CanTrigger(evt.Type))
                    effect.Execute(new EffectContext(this, evt));
            }
    }

    public override void Initialize()
    {
        Definition = (MinionType)SourceCard.cardType;
        CurrentHealth = Definition.baseHealth;
        CurrentAttack = Definition.baseAttack;
    }
}