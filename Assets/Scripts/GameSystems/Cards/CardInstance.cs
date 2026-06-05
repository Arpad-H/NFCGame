using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameSystems;
using UnityEngine;

//instance of a card type like a minion on the board or a spell 
public abstract class CardInstance
{
    public CardData SourceCard;
    public Player Owner;
    public Player Opponent;
    public Board Board;

    public virtual Task HandleEvent(GameEvent evt)
    {
        return Task.CompletedTask;
    }
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

public class FieldableCardInstance : CardInstance<FieldableCardInstance>, IAudioOnGameEventReceiver
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

    public async Task AttachCardToThis(Rune[] otherRunes)
    {
        if (otherRunes == null) return;

        if (SourceCard?.cardType is not FieldableCardType fType) return;
        if (otherRunes.Length > 0 && fType.effectActivatingRunes is { Length: > 0 })
        {
            if (!(otherRunes[0] == Rune.None || fType.effectActivatingRunes[0] == Rune.None))
            {
                if (otherRunes[0] == fType.effectActivatingRunes[0])
                {
                    IsFieldActive[1] = true;
                    await HandleEvent(new GameEvent(GameEventType.OnActivateEffectEvent, this,
                        EffectFieldPosition.Effect1));
                }
            }
        }

        if (otherRunes.Length <= 1 || fType.effectActivatingRunes is not { Length: > 1 }) return;
        if (otherRunes[1] == Rune.None || fType.effectActivatingRunes[1] == Rune.None) return;
        if (otherRunes[1] == fType.effectActivatingRunes[1])
        {
            IsFieldActive[2] = true;
            await HandleEvent(new GameEvent(GameEventType.OnActivateEffectEvent, this, EffectFieldPosition.Effect2));
        }
    }


    public async Task DetachCardFromThis()
    {
        IsFieldActive[1] = false;
        await HandleEvent(new GameEvent(GameEventType.OnDeactivateEffectEvent, this, 1));
        IsFieldActive[2] = false;
        await HandleEvent(new GameEvent(GameEventType.OnDeactivateEffectEvent, this, 2));
    }


    public virtual void Initialize()
    {
    }

    public void HandleAudioOnEvent(GameEvent evt)
    {
        foreach (AudioOnEvent audioOnEvent in SourceCard.audioOnEvents)
        {
            audioOnEvent.TryPlayAudio(evt);
        }
    }

    public Task ReturnToHand()
    {
        Owner.ReturnCardToHand(this);
        return Task.CompletedTask;
    }
}

public class MinionInstance : FieldableCardInstance, ITargetable, IGameEventReceiver
{
    private MinionType Definition;
    public int BaseHealth => Definition.baseHealth;
    public int BaseAttack => Definition.baseAttack;
    private int CurrentHealth { get; set; }
    public int CurrentAttack { get; private set; }
    public int BonusHealth = 0;
    public int BonusAttack = 0;
    public event Action<int, int> OnStatsChanged;
    public event Action OnDeath;
    public event Action<StatusEffectInstance> OnStatusEffectAdded;
    public event Action<StatusEffectInstance> OnStatusEffectRemoved;

    public List<StatusEffectInstance> statusEffects = new();

    public async Task TakeDamage(DamageEventData damageEventData)
    {
        await HandleEvent(new GameEvent(GameEventType.OnAboutToTakeDamage, this, damageEventData));
        if (damageEventData.IsPrevented) return;

        BonusHealth -= damageEventData.Amount;
        if (BonusHealth <= 0)
        {
            CurrentHealth += BonusHealth; //apply leftover damage to current health
            BonusHealth = 0;
        }
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
        await HandleEvent(new GameEvent(GameEventType.OnDamaged, this, damageEventData.Source));
        if (CurrentHealth <= 0)
        {
            await HandleEvent(new GameEvent(GameEventType.OnKilled, this, damageEventData.Source));
            OnDeath?.Invoke();
        }
    }
    public async Task Heal(HealEventData healEventData)
    {
        await HandleEvent(new GameEvent(GameEventType.OnAboutToBeHealed, this, healEventData));
        if (healEventData.IsPrevented) return;

        CurrentHealth += healEventData.Amount;
        int overheal = CurrentHealth - BaseHealth;
        if (overheal < 0) overheal = 0;
         
        if (CurrentHealth > BaseHealth) CurrentHealth = BaseHealth;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
        await HandleEvent(new GameEvent(GameEventType.OnHealed, this, healEventData.Source));
    }

    public Task ModifyStat(MinionStats stat, int amount)
    {
        switch (stat)
        {
            case MinionStats.Attack:
                CurrentAttack += amount;
                break;
            case MinionStats.Health:
                CurrentHealth += amount;
                break;
        }

        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
        return Task.CompletedTask;
    }

    public override async Task HandleEvent(GameEvent evt)
    {
        var activeTriggers = GetActiveTriggers();
        var statusEffectsSnapshot = statusEffects.ToList();
        //StatusEffects intercept game events first
        foreach (var statusEffect in statusEffectsSnapshot)
        {
            await statusEffect.HandleEvent(evt, this);
        }

        HandleAudioOnEvent(evt);
        foreach (var binding in activeTriggers)
        {
            if (binding.Trigger != null &&
                binding.Trigger.CanTrigger(evt, binding))
            {
                await binding.Trigger.Execute(
                    new EffectContext(this, evt));
            }
        }
    }

    private List<TriggerBinding> GetActiveTriggers()
    {
        List<TriggerBinding> activeTriggers = new();

        if (IsFieldActive[0])
        {
            activeTriggers.AddRange(
                Definition.PassiveEventTriggers.Select(t =>
                    new TriggerBinding
                    {
                        Trigger = t,
                        EffectIndex = EffectFieldPosition.Passive
                    }));
        }

        if (IsFieldActive[1])
        {
            activeTriggers.AddRange(
                Definition.Effect1EventTriggers.Select(t =>
                    new TriggerBinding
                    {
                        Trigger = t,
                        EffectIndex = EffectFieldPosition.Effect1
                    }));
        }

        if (IsFieldActive[2])
        {
            activeTriggers.AddRange(
                Definition.Effect2EventTriggers.Select(t =>
                    new TriggerBinding
                    {
                        Trigger = t,
                        EffectIndex = EffectFieldPosition.Effect2
                    }));
        }

        activeTriggers.Add(new TriggerBinding(Definition.DefaultCombatBehaviour,
            EffectFieldPosition.OnCombatResolveEffect));

        return activeTriggers;
    }


    public override void Initialize()
    {
        Definition = (MinionType)SourceCard.cardType;
        CurrentHealth = Definition.baseHealth;
        CurrentAttack = Definition.baseAttack;
    }

    public async Task ApplyStatusEffect(StatusEffectInstance statusEffectInstance)
    {
        statusEffects.Add(statusEffectInstance);
        await HandleEvent(new GameEvent(GameEventType.OnStatusEffectApplied, this, statusEffectInstance));
        OnStatusEffectAdded?.Invoke(statusEffectInstance);
    }

    public async Task RemoveStatusEffect(StatusEffectData statusEffectInstance)
    {
        //figure out the effect instance that matches the data name
        StatusEffectInstance toRemove = null;
        foreach (var statusEffect in statusEffects)
        {
            if (statusEffect.Data.effectName == statusEffectInstance.effectName)
            {
                toRemove = statusEffect;
                break;
            }
        }

        await RemoveStatusEffect(toRemove);
    }

    public async Task RemoveStatusEffect(StatusEffectInstance statusEffectInstance)
    {
        if (statusEffects.Remove(statusEffectInstance))
        {
            await HandleEvent(new GameEvent(GameEventType.OnStatusEffectRemoved, this, statusEffectInstance));
            OnStatusEffectRemoved?.Invoke(statusEffectInstance);
        }
    }

    public bool HasStatusEffect(StatusEffectType statusEffectType)
    {
        foreach (StatusEffectInstance se in statusEffects)
        {
            if (se.Data.effectName == statusEffectType)
            {
                return true;
            }
        }

        return false;
    }
}

public class SpellInstance : FieldableCardInstance, IGameEventReceiver
    {
        private SpellType Definition;

        public override async Task HandleEvent(GameEvent evt)
        {
            var activeTriggers = GetActiveTriggers();
            HandleAudioOnEvent(evt);
            foreach (var binding in activeTriggers)
            {
                if (binding.Trigger != null &&
                    binding.Trigger.CanTrigger(evt, binding))
                {
                    await binding.Trigger.Execute(
                        new EffectContext(this, evt));
                }
            }
        }

        private List<TriggerBinding> GetActiveTriggers()
        {
            List<TriggerBinding> activeTriggers = new();


            activeTriggers.AddRange(
                Definition.SpellEffects.Select(t =>
                    new TriggerBinding
                    {
                        Trigger = t,
                        EffectIndex = EffectFieldPosition.Passive
                    }));


            return activeTriggers;
        }

        public override void Initialize()
        {
            Definition = (SpellType)SourceCard.cardType;
        }
    }

    public class ItemInstance : FieldableCardInstance, IGameEventReceiver
    {
        public MinionInstance ItemHolder { get; set; }
        private ItemType Definition;

        public override async Task HandleEvent(GameEvent evt)
        {
            var activeTriggers = GetActiveTriggers();
            HandleAudioOnEvent(evt);
            foreach (var binding in activeTriggers)
            {
                if (binding.Trigger != null &&
                    binding.Trigger.CanTrigger(evt, binding))
                {
                    await binding.Trigger.Execute(
                        new EffectContext(this, evt));
                }
            }
        }

        private List<TriggerBinding> GetActiveTriggers()
        {
            List<TriggerBinding> activeTriggers = new();

            if (IsFieldActive[0])
            {
                activeTriggers.AddRange(
                    Definition.PassiveEventTriggers.Select(t =>
                        new TriggerBinding
                        {
                            Trigger = t,
                            EffectIndex = EffectFieldPosition.Passive
                        }));
            }

            if (IsFieldActive[1])
            {
                activeTriggers.AddRange(
                    Definition.Effect1EventTriggers.Select(t =>
                        new TriggerBinding
                        {
                            Trigger = t,
                            EffectIndex = EffectFieldPosition.Effect1
                        }));
            }

            if (IsFieldActive[2])
            {
                activeTriggers.AddRange(
                    Definition.Effect2EventTriggers.Select(t =>
                        new TriggerBinding
                        {
                            Trigger = t,
                            EffectIndex = EffectFieldPosition.Effect2
                        }));
            }

            return activeTriggers;
        }

        public override void Initialize()
        {
            Definition = (ItemType)SourceCard.cardType;
        }

        public Rune[] GetSuppliedRunes()
        {
            if (SourceCard?.cardType is ItemType fType)
            {
                return fType.suppliedActivatorRunes;
            }

            return null;
        }
    }