using System;
using System.Collections.Generic;
using GameSystems;

//instance of a card type like a minion on the board or a spell 
public abstract class CardInstance
{
    public CardData SourceCard;
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
    
    public int SummonedOnRound;
   
    public bool[]
        isFieldActive =
        {
            true, false, false
        }; // 0 = crown, 1 = core, 2 = root. crown and core get covered first. root is always active (unless disabled by some effect possibly in the future)

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

    public void AttachCardToThis(Rune[] otherRunes)
    {
        if (otherRunes == null) return;

        if (SourceCard?.cardType is not FieldableCardType fType) return;
        if (otherRunes.Length > 0 && fType.effectActivatingRunes != null && fType.effectActivatingRunes.Length > 0)
        {
            if (!(otherRunes[0] == Rune.None || fType.effectActivatingRunes[0] == Rune.None))
            {
                if (otherRunes[0] == fType.effectActivatingRunes[0])
                    isFieldActive[1] = true;
            }
              
        }
        if (otherRunes.Length <= 1 || fType.effectActivatingRunes is not { Length: > 1 }) return;
        if (otherRunes[1] == Rune.None || fType.effectActivatingRunes[1] == Rune.None) return;
        if (otherRunes[1] == fType.effectActivatingRunes[1]) isFieldActive[2] = true;
    }
  

    public void DetachCardFromThis()
    {
        isFieldActive[1] = false;
        isFieldActive[2] = false;
    }

    public virtual void Initialize()
    {
    }
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
        HandleEvent (new GameEvent(GameEventType.OnAboutToTakeDamage, this, damageEventData));
        if (damageEventData.IsPrevented) return;
        
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
        var activeTriggers = GetActiveTriggers();
        foreach (IEventTrigger effect in activeTriggers)
        {
            if (effect != null && effect.CanTrigger(evt.Type))
                effect.Execute(new EffectContext(this, evt));
        }
    }

    public List<IEventTrigger> GetActiveTriggers()
    {
        List<IEventTrigger> activeTriggers = new List<IEventTrigger>();

        if (isFieldActive[0])
            activeTriggers.AddRange(Definition.PassiveEventTriggers);
        if (isFieldActive[1])
            activeTriggers.AddRange(Definition.Effect1EventTriggers);
        if (isFieldActive[2])
            activeTriggers.AddRange(Definition.Effect2EventTriggers);
        activeTriggers.Add(Definition.DefaultCombatBehaviour);
        return activeTriggers;
    }

    public override void Initialize()
    {
        Definition = (MinionType)SourceCard.cardType;
        CurrentHealth = Definition.baseHealth;
        CurrentAttack = Definition.baseAttack;
    }
}

public class SpellOrItemInstance : FieldableCardInstance, IGameEventReceiver
{
    public SpellOrItemType Definition;

    public void HandleEvent(GameEvent evt)
    {
        var activeTriggers = GetActiveTriggers();
        foreach (IEventTrigger effect in activeTriggers)
        {
            if (effect != null && effect.CanTrigger(evt.Type))
                effect.Execute(new EffectContext(this, evt));
        }
    }

    public List<IEventTrigger> GetActiveTriggers()
    {
        List<IEventTrigger> activeTriggers = new List<IEventTrigger>();

        if (isFieldActive[0])
            activeTriggers.AddRange(Definition.PassiveEventTriggers);
        if (isFieldActive[1])
            activeTriggers.AddRange(Definition.Effect1EventTriggers);
        if (isFieldActive[2])
            activeTriggers.AddRange(Definition.Effect2EventTriggers);

        return activeTriggers;
    }

    public override void Initialize()
    {
        Definition = (SpellOrItemType)SourceCard.cardType;
    }
    public Rune[] GetSuppliedRunes()
    {
        if (SourceCard?.cardType is SpellOrItemType fType)
        {
            return fType.suppliedActivatorRunes;
        }
        return null;
    }
}