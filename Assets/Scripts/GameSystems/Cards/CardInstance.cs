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
        }; // 0 = crown/passive (always active), 1 = core/Effect1, 2 = root/Effect2

    // Built once per instance at Initialize(). Bindings persist for the card's
    // lifetime; field activation flips IsActive instead of rebuilding the list,
    // so per-instance trigger state (StateSlot) survives rune attach/detach.
    public List<TriggerBinding> Bindings { get; protected set; } = new();

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
                    RefreshBindingActivity();
                    await HandleEvent(new GameEvent(GameEventType.OnActivateEffectEvent, this,
                        new EffectFieldEventData(EffectFieldPosition.Effect1)));
                }
            }
        }

        if (otherRunes.Length <= 1 || fType.effectActivatingRunes is not { Length: > 1 }) return;
        if (otherRunes[1] == Rune.None || fType.effectActivatingRunes[1] == Rune.None) return;
        if (otherRunes[1] == fType.effectActivatingRunes[1])
        {
            IsFieldActive[2] = true;
            RefreshBindingActivity();
            await HandleEvent(new GameEvent(GameEventType.OnActivateEffectEvent, this,
                new EffectFieldEventData(EffectFieldPosition.Effect2)));
        }
    }

    public async Task DetachCardFromThis()
    {
        IsFieldActive[1] = false;
        RefreshBindingActivity();
        await HandleEvent(new GameEvent(GameEventType.OnDeactivateEffectEvent, this,
            new EffectFieldEventData(EffectFieldPosition.Effect1)));
        IsFieldActive[2] = false;
        RefreshBindingActivity();
        await HandleEvent(new GameEvent(GameEventType.OnDeactivateEffectEvent, this,
            new EffectFieldEventData(EffectFieldPosition.Effect2)));
    }

    public virtual void Initialize()
    {
        if (SourceCard?.cardType is FieldableCardType fType)
        {
            Bindings = BuildBindings(fType, IsFieldActive);
        }
    }

    // Single shared builder for all fieldable card types. One TriggerBinding per
    // trigger per instance; the active[] flags map to Passive/Effect1/Effect2.
    protected static List<TriggerBinding> BuildBindings(FieldableCardType type, bool[] active)
    {
        var bindings = new List<TriggerBinding>();
        AppendBindings(bindings, type.PassiveEventTriggers, EffectFieldPosition.Passive, active[0]);
        AppendBindings(bindings, type.Effect1EventTriggers, EffectFieldPosition.Effect1, active[1]);
        AppendBindings(bindings, type.Effect2EventTriggers, EffectFieldPosition.Effect2, active[2]);
        return bindings;
    }

    // Lower-level overload for trigger lists that don't live on a
    // FieldableCardType (e.g. SpellType.SpellEffects).
    protected static void AppendBindings(List<TriggerBinding> bindings, List<IEventTrigger> triggers,
        EffectFieldPosition position, bool isActive)
    {
        foreach (var trigger in triggers)
        {
            bindings.Add(new TriggerBinding(trigger, position) { IsActive = isActive });
        }
    }

    // Keeps binding activity in sync with IsFieldActive after rune attach/detach.
    // Bindings outside the three effect fields (e.g. combat behaviour) are untouched.
    protected void RefreshBindingActivity()
    {
        foreach (var binding in Bindings)
        {
            binding.IsActive = binding.EffectIndex switch
            {
                EffectFieldPosition.Passive => IsFieldActive[0],
                EffectFieldPosition.Effect1 => IsFieldActive[1],
                EffectFieldPosition.Effect2 => IsFieldActive[2],
                _ => binding.IsActive,
            };
        }
    }

    // Shared trigger dispatch, replacing the per-event GetActiveTriggers() rebuilds.
    protected async Task RunTriggers(GameEvent evt)
    {
        foreach (var binding in Bindings)
        {
            if (!binding.IsActive || binding.Trigger == null) continue;
            if (binding.Trigger.CanTrigger(evt, binding))
            {
                await binding.Trigger.Execute(new EffectContext(this, evt));
            }
        }
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
        // Leaving the field by any path ends this card's continuous effects.
        Board?.AuraRegistry.UnregisterAllFrom(this);
        Owner.ReturnCardToHand(this);
        return Task.CompletedTask;
    }
}

public class MinionInstance : FieldableCardInstance, ITargetable, IGameEventReceiver
{
    private MinionType Definition;
    public int BaseHealth => Definition.baseHealth;
    public int BaseAttack => Definition.baseAttack;

    // All stat buffs/debuffs live here as sourced, reversible modifiers.
    // MaxHealth/CurrentAttack are derived; CurrentHealth tracks damage taken.
    private readonly List<StatModifier> modifiers = new();
    public int MaxHealth => BaseHealth + modifiers.Sum(m => m.Health);
    public int CurrentHealth { get; private set; }
    public int CurrentAttack => Mathf.Max(0, BaseAttack + modifiers.Sum(m => m.Attack));

    public event Action<int, int> OnStatsChanged;
    public event Action OnDeath;
    public event Action<StatusEffectInstance> OnStatusEffectAdded;
    public event Action<StatusEffectInstance> OnStatusEffectRemoved;

    public List<StatusEffectInstance> statusEffects = new();

    public bool IsAlive => CurrentHealth > 0;

    // Temporary damage absorption. Consumed before health; granted by
    // AddShieldEffect ("block the next N damage", "1 shield per ally").
    public int Shield { get; set; }

    public async Task TakeDamage(DamageEventData damageEventData)
    {
        // Interception stays synchronous and entity-local: the payload is
        // mutable (IsPrevented) and must resolve before damage is applied.
        await HandleEvent(new GameEvent(GameEventType.OnAboutToTakeDamage, this, damageEventData));
        if (damageEventData.IsPrevented) return;
        if (damageEventData.Source is MinionInstance )
        {
            AudioManager.Instance.PlayMinionClashSound();
        }
        // Shield soaks damage first; only the remainder reaches health.
        int remaining = damageEventData.Amount;
        if (Shield > 0)
        {
            int absorbed = Mathf.Min(Shield, remaining);
            Shield -= absorbed;
            remaining -= absorbed;
        }

        CurrentHealth -= remaining;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);

        // Taking actual damage wakes a sleeping unit.
        if (remaining > 0 && HasStatusEffect(StatusEffectType.Sleep))
        {
            await RemoveStatusEffect(StatusEffectType.Sleep);
        }

        // Death is recorded first so the drain that OnDamaged starts (or the
        // already-running one) processes it after the queue empties. OnKilled
        // is raised by the board's death batch, not here.
        if (CurrentHealth <= 0)
        {
            Board.ReportDeath(this, damageEventData.Source);
        }

        // Fully shielded hits raise no OnDamaged — nothing was damaged, so
        // reflect/retaliate triggers must not fire.
        if (remaining > 0)
        {
            await Board.RaiseReaction(new GameEvent(GameEventType.OnDamaged, this,
                new SourceEventData(damageEventData.Source, remaining)));
        }
    }

    // Used by ReviveEffect during the death batch: restores the minion to full
    // health so it survives corpse removal (Board skips revived minions).
    public void RestoreToFullHealth()
    {
        CurrentHealth = MaxHealth;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
    }

    public async Task Heal(HealEventData healEventData)
    {
        await HandleEvent(new GameEvent(GameEventType.OnAboutToBeHealed, this, healEventData));
        if (healEventData.IsPrevented) return;

        CurrentHealth += healEventData.Amount;
        if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
        await Board.RaiseReaction(new GameEvent(GameEventType.OnHealed, this,
            new SourceEventData(healEventData.Source, healEventData.Amount)));
    }

    // Called by Board after the death batch's OnKilled events have been
    // delivered. Fires OnDeath, which removes the minion from its portal.
    public void ProcessDeath()
    {
        OnDeath?.Invoke();
    }

    // Permanent (unsourced) buff — routes through the modifier list so all
    // stat math stays in one place. Used by ModifyStatsEffect.
    public Task ModifyStat(MinionStats stat, int amount)
    {
        switch (stat)
        {
            case MinionStats.Attack:
                AddModifier(new StatModifier(null, 0, amount));
                break;
            case MinionStats.Health:
                AddModifier(new StatModifier(null, amount, 0));
                break;
        }

        return Task.CompletedTask;
    }

    // Hearthstone semantics: +max HP also raises current HP by the same amount;
    // losing the modifier clamps current HP down to the new max (damage taken
    // is preserved, the buffed portion is lost).
    public void AddModifier(StatModifier modifier)
    {
        modifiers.Add(modifier);
        if (modifier.Health > 0) CurrentHealth += modifier.Health;
        else if (modifier.Health < 0 && CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
    }

    public void RemoveModifier(StatModifier modifier)
    {
        if (!modifiers.Remove(modifier)) return;
        if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
    }

    public void RemoveModifiersFrom(object source)
    {
        var removed = modifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
        if (removed == 0) return;
        if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
    }

    // In-place change for auras whose amount varies (e.g. +1 HP per rat):
    // applies only the delta so damage taken is never healed back by a refresh.
    public void AdjustModifier(StatModifier modifier, int newHealth, int newAttack)
    {
        int healthDelta = newHealth - modifier.Health;
        modifier.Health = newHealth;
        modifier.Attack = newAttack;
        if (healthDelta > 0) CurrentHealth += healthDelta;
        else if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
    }

    public List<StatModifier> GetModifiersFrom(object source)
    {
        return modifiers.Where(m => ReferenceEquals(m.Source, source)).ToList();
    }

    public override async Task HandleEvent(GameEvent evt)
    {
        var statusEffectsSnapshot = statusEffects.ToList();
        foreach (var statusEffect in statusEffectsSnapshot)
        {
            await statusEffect.HandleEvent(evt, this);
        }

        HandleAudioOnEvent(evt);
        await RunTriggers(evt);
    }

    public override void Initialize()
    {
        Definition = (MinionType)SourceCard.cardType;
        CurrentHealth = Definition.baseHealth;
        base.Initialize();
        // Combat behaviour is always live regardless of rune field state.
        Bindings.Add(new TriggerBinding(Definition.DefaultCombatBehaviour,
            EffectFieldPosition.OnCombatResolveEffect));
    }

    public async Task ApplyStatusEffect(StatusEffectInstance statusEffectInstance)
    {
        // A minion may only hold one status effect of each type. If it already
        // has this type, just reapply (refresh its duration) instead of adding a
        // duplicate. This deliberately does not fire OnStatusEffectRemoved.
        StatusEffectInstance existing = null;
        foreach (var statusEffect in statusEffects)
        {
            if (statusEffect.Data.effectName == statusEffectInstance.Data.effectName)
            {
                existing = statusEffect;
                break;
            }
        }

        if (existing != null)
        {
            existing.DurationRemaining = statusEffectInstance.DurationRemaining;
            existing.Source = statusEffectInstance.Source;
            await HandleEvent(new GameEvent(GameEventType.OnStatusEffectApplied, this,
                new StatusEffectEventData(existing)));
            return;
        }

        statusEffects.Add(statusEffectInstance);
        await HandleEvent(new GameEvent(GameEventType.OnStatusEffectApplied, this,
            new StatusEffectEventData(statusEffectInstance)));
        OnStatusEffectAdded?.Invoke(statusEffectInstance);
    }

    public async Task RemoveStatusEffect(StatusEffectData statusEffectData)
    {
        await RemoveStatusEffect(statusEffectData.effectName);
    }

    public async Task RemoveStatusEffect(StatusEffectType statusEffectType)
    {
        StatusEffectInstance toRemove = null;
        foreach (var statusEffect in statusEffects)
        {
            if (statusEffect.Data.effectName == statusEffectType)
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
            await HandleEvent(new GameEvent(GameEventType.OnStatusEffectRemoved, this,
                new StatusEffectEventData(statusEffectInstance)));
            OnStatusEffectRemoved?.Invoke(statusEffectInstance);
        }
    }

    public bool HasStatusEffect(StatusEffectType statusEffectType)
    {
        foreach (StatusEffectInstance se in statusEffects)
        {
            if (se.Data.effectName == statusEffectType) return true;
        }

        return false;
    }

    public string ToString()
    {
        return $"{SourceCard.cardName} (HP: {CurrentHealth}/{MaxHealth}, ATK: {CurrentAttack}, SHIELD: {Shield})";
    }
}

public class SpellInstance : FieldableCardInstance, IGameEventReceiver
{
    private SpellType Definition;

    public override async Task HandleEvent(GameEvent evt)
    {
        HandleAudioOnEvent(evt);
        await RunTriggers(evt);
    }

    public override void Initialize()
    {
        Definition = (SpellType)SourceCard.cardType;
        // SpellType is not a FieldableCardType, so build directly from SpellEffects.
        Bindings = new List<TriggerBinding>();
        AppendBindings(Bindings, Definition.SpellEffects, EffectFieldPosition.Passive, true);
    }
}

public class ItemInstance : FieldableCardInstance, IGameEventReceiver
{
    public MinionInstance ItemHolder { get; set; }
    private ItemType Definition;

    public override async Task HandleEvent(GameEvent evt)
    {
        HandleAudioOnEvent(evt);
        await RunTriggers(evt);
    }

    public override void Initialize()
    {
        Definition = (ItemType)SourceCard.cardType;
        base.Initialize();
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
