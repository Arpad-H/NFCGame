using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameSystems;

//instance of a card type like a minion on the board or a spell 
public abstract class CardInstance
{
    public CardData SourceCard;
    public Player Owner;
    public Player Opponent;
    public Board Board;
    public virtual Task HandleEvent(GameEvent evt){ return Task.CompletedTask; }
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
        IsFieldActive =
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
        if (otherRunes.Length > 0 && fType.effectActivatingRunes is { Length: > 0 })
        {
            if (!(otherRunes[0] == Rune.None || fType.effectActivatingRunes[0] == Rune.None))
            {
                if (otherRunes[0] == fType.effectActivatingRunes[0])
                    IsFieldActive[1] = true;
            }
              
        }
        if (otherRunes.Length <= 1 || fType.effectActivatingRunes is not { Length: > 1 }) return;
        if (otherRunes[1] == Rune.None || fType.effectActivatingRunes[1] == Rune.None) return;
        if (otherRunes[1] == fType.effectActivatingRunes[1]) IsFieldActive[2] = true;
    }
  

    public void DetachCardFromThis()
    {
        IsFieldActive[1] = false;
        IsFieldActive[2] = false;
    }

    public virtual void Initialize()
    {
    }

  
}

public class MinionInstance : FieldableCardInstance, ITargetable, IGameEventReceiver
{
    private MinionType Definition;
    private int CurrentHealth { get;  set; }
    public int CurrentAttack { get; private set; }
    public event Action<int> OnHealthChanged;
    public event Action OnDeath;

    public async Task TakeDamage(DamageEventData damageEventData)
    {
        await HandleEvent(new GameEvent(GameEventType.OnAboutToTakeDamage, this, damageEventData));
        if (damageEventData.IsPrevented) return;
        await Task.Delay(500); //TODO replace with animation event trigger
        CurrentHealth -= damageEventData.Amount;
        OnHealthChanged?.Invoke(CurrentHealth);
        await HandleEvent(new GameEvent(GameEventType.OnDamaged, this, damageEventData.Source));
        if (CurrentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    public override async Task HandleEvent(GameEvent evt)
    {
        var activeTriggers = GetActiveTriggers();
        foreach (IEventTrigger effect in activeTriggers)
        {
            if (effect != null && effect.CanTrigger(evt.Type))
                await effect.Execute(new EffectContext(this, evt));
        }
    }

    private List<IEventTrigger> GetActiveTriggers()
    {
        List<IEventTrigger> activeTriggers = new List<IEventTrigger>();

        if (IsFieldActive[0])
            activeTriggers.AddRange(Definition.PassiveEventTriggers);
        if (IsFieldActive[1])
            activeTriggers.AddRange(Definition.Effect1EventTriggers);
        if (IsFieldActive[2])
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
    private SpellOrItemType Definition;

    public override async Task HandleEvent(GameEvent evt)
    {
        var activeTriggers = GetActiveTriggers();
        foreach (IEventTrigger effect in activeTriggers)
        {
            if (effect != null && effect.CanTrigger(evt.Type))
                await effect.Execute(new EffectContext(this, evt));
        }
    }

    private List<IEventTrigger> GetActiveTriggers()
    {
        List<IEventTrigger> activeTriggers = new List<IEventTrigger>();

        if (IsFieldActive[0])
            activeTriggers.AddRange(Definition.PassiveEventTriggers);
        if (IsFieldActive[1])
            activeTriggers.AddRange(Definition.Effect1EventTriggers);
        if (IsFieldActive[2])
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