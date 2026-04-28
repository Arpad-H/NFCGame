using System;
using System.Collections.Generic;

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
    public bool [] isFieldCovered = new bool[3]; // 0 = crown, 1 = core, 2 = root. crown and core get covered first. root is always active (unless disabled by some effect possibly in the future)

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

    public void PlaceCardOnTop(bool runesMatching = false)// runes for now always false so it covers up root and core. Matching runes would mean it only covers up root, leaving core active
    {
        if (runesMatching)
        {
            isFieldCovered[0] = true; //cover crown
        }
        else
        {
            isFieldCovered[1] = true; //cover core
            isFieldCovered[0] = true; //cover crown
        }
    }
    public void RemoveCardFromTop()
    {
        isFieldCovered[1] = false; //uncover core
        isFieldCovered[0] = false; //uncover crown
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
        {
            var activeTriggers = GetActiveTriggers();
            foreach (IEventTrigger effect in activeTriggers)
            {
                if (effect != null && effect.CanTrigger(evt.Type))
                    effect.Execute(new EffectContext(this, evt));
            }
        }
           
    }
    public List<IEventTrigger> GetActiveTriggers()
    {
        List<IEventTrigger> activeTriggers = new List<IEventTrigger>();

        if (isFieldCovered[0])
            activeTriggers.AddRange(Definition.CrownEventTriggers);
        if (isFieldCovered[1])
            activeTriggers.AddRange(Definition.CoreEventTriggers);
        if (isFieldCovered[2])
            activeTriggers.AddRange(Definition.RootEventTriggers);

        return activeTriggers;
    }

    public override void Initialize()
    {
        Definition = (MinionType)SourceCard.cardType;
        CurrentHealth = Definition.baseHealth;
        CurrentAttack = Definition.baseAttack;
    }
}