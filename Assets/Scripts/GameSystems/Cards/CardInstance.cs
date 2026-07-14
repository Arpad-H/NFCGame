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

    // Distinguishes two copies of the same card in logs — without it a trigger
    // loop between two Rats reads as "Rat hits Rat hits Rat".
    private static int nextInstanceId = 1;
    public readonly int InstanceId = nextInstanceId++;

    public virtual Task HandleEvent(GameEvent evt)
    {
        return Task.CompletedTask;
    }

    // MUST stay `override`: string interpolation dispatches virtually, so a
    // non-virtual `public string ToString()` would silently print the type name.
    public override string ToString()
    {
        string name = SourceCard != null ? SourceCard.cardName : GetType().Name;
        return Owner != null ? $"{name}#{InstanceId}(P{Owner.playerId})" : $"{name}#{InstanceId}";
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

    // Monotonic order in which this card was fielded (stamped by Board.PlaceCard).
    // Lets effects find "the most recently placed card still on the board" without
    // holding a reference that could go stale once the card leaves play.
    public long PlacementSequence;

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
        // Fire the deactivate event while the field's bindings are STILL active,
        // THEN mark the field inactive. RunTriggers skips inactive bindings, so
        // dispatching before the flip is what lets a field's own
        // OnEffectFieldIsDeActivated cleanup (RemoveModifierEffect /
        // UnregisterAuraEffect) actually run as the field turns off.
        await HandleEvent(new GameEvent(GameEventType.OnDeactivateEffectEvent, this,
            new EffectFieldEventData(EffectFieldPosition.Effect1)));
        IsFieldActive[1] = false;
        RefreshBindingActivity();

        await HandleEvent(new GameEvent(GameEventType.OnDeactivateEffectEvent, this,
            new EffectFieldEventData(EffectFieldPosition.Effect2)));
        IsFieldActive[2] = false;
        RefreshBindingActivity();
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
            binding.Owner ??= this;
            if (binding.Trigger.CanTrigger(evt, binding))
            {
                await binding.Trigger.Execute(new EffectContext(this, evt));
            }
        }
    }

    // Plays every clip bound to this event and returns the longest clip's
    // length in seconds (0 when nothing played).
    public float HandleAudioOnEvent(GameEvent evt)
    {
        float longest = 0f;
        if (SourceCard?.audioOnEvents == null) return longest;
        foreach (AudioOnEvent audioOnEvent in SourceCard.audioOnEvents)
        {
            longest = Mathf.Max(longest, audioOnEvent.TryPlayAudio(evt));
        }

        return longest;
    }

    // Time (in Time.time) at which an early-started "on played" clip finishes,
    // or -1 when it hasn't been started early. See StartOnPlayedAudio.
    private float onPlayedAudioEndTime = -1f;

    // Fires the "on played" SFX before the OnPlayed event itself is raised.
    // Portal calls this the moment a minion erupts from the portal, so the clip
    // runs under the spawn animation instead of starting after the unit lands.
    public void StartOnPlayedAudio()
    {
        if (onPlayedAudioEndTime >= 0f) return;
        float length = HandleAudioOnEvent(new GameEvent(GameEventType.OnPlayed, this));
        onPlayedAudioEndTime = Time.time + length;
    }

    // Plays this event's audio, then — when the card is first played — holds
    // for the clip's duration so its "on played" SFX isn't cut off the instant
    // the card's effects start resolving in the same call. Other events resolve
    // immediately to keep combat snappy.
    protected async Task PlayEventAudioAndDelayOnPlayed(GameEvent evt)
    {
        if (evt.GetEventType() != GameEventType.OnPlayed)
        {
            HandleAudioOnEvent(evt);
            return;
        }

        // Already started under the spawn animation: only wait for the tail
        // that outlasts it, rather than restarting the clip from the top.
        float remaining = onPlayedAudioEndTime >= 0f
            ? onPlayedAudioEndTime - Time.time
            : HandleAudioOnEvent(evt);

        if (remaining > 0f) await Task.Delay(Mathf.CeilToInt(remaining * 1000f));
    }

    public async Task ReturnToHand()
    {
        // Leaving the field by any path ends this card's continuous effects.
        Board?.AuraRegistry.UnregisterAllFrom(this);
        if (Announcer.Instance != null) await Announcer.Instance.AnnounceReturnCard();
        await Owner.ReturnCardToHand(this);
    }

    // Replaces this instance's trigger bindings with the given card's
    // passive/E1/E2 lists (fresh per-instance state) — the copy half of Mirror.
    // SourceCard is NOT swapped: name, art, audio, and printed runes stay this
    // card's own; only the behaviour is copied. The copied battlecry-style
    // passives then fire via a local OnPlayed, and any effect field that was
    // already rune-activated re-announces so adopted OnEffectFieldIsActivated
    // triggers (auras etc.) don't miss the activation that happened pre-copy.
    public async Task AdoptTriggersFrom(CardData other)
    {
        if (other?.cardType is not FieldableCardType otherType)
        {
            Debug.LogError($"AdoptTriggersFrom: {other?.cardName ?? "null"} has no fieldable card type to copy.");
            return;
        }

        Bindings = BuildBindings(otherType, IsFieldActive);
        Debug.Log($"{this} adopted the triggers of {other.cardName}.");

        await HandleEvent(new GameEvent(GameEventType.OnPlayed, this));

        if (IsFieldActive[1])
        {
            await HandleEvent(new GameEvent(GameEventType.OnActivateEffectEvent, this,
                new EffectFieldEventData(EffectFieldPosition.Effect1)));
        }

        if (IsFieldActive[2])
        {
            await HandleEvent(new GameEvent(GameEventType.OnActivateEffectEvent, this,
                new EffectFieldEventData(EffectFieldPosition.Effect2)));
        }
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
    // (amount, isClashHit) — isClashHit tells presentation the minion was
    // overlapping its opponent in the middle of the lane when the blow landed.
    public event Action<int, bool> OnDamageDealt;
    public event Action<int> OnHealReceived;

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
        if (damageEventData.Source is MinionInstance && !damageEventData.IsClashHit)
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
        if (remaining > 0)
            OnDamageDealt?.Invoke(remaining, damageEventData.IsClashHit);

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

        int prevHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(CurrentHealth + healEventData.Amount, MaxHealth);
        int actualHealed = CurrentHealth - prevHealth;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
        if (actualHealed > 0)
            OnHealReceived?.Invoke(actualHealed);

        // Mirrors OnDamaged (which only fires when damage actually landed): a
        // heal at full health raises no OnHealed, and the reaction carries the
        // amount actually restored, not the amount requested. Without this,
        // heal-on-heal triggers self-sustain forever at full HP.
        if (actualHealed > 0)
        {
            await Board.RaiseReaction(new GameEvent(GameEventType.OnHealed, this,
                new SourceEventData(healEventData.Source, actualHealed)));
        }
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

        await PlayEventAudioAndDelayOnPlayed(evt);
        await RunTriggers(evt);
    }

    public override void Initialize()
    {
        Definition = (MinionType)SourceCard.cardType;
        CurrentHealth = Definition.baseHealth;
        base.Initialize();
    }

    public async Task ApplyStatusEffect(StatusEffectInstance statusEffectInstance)
    {
        // A minion may only hold one instance of each status ASSET. Reapplying
        // the same asset refreshes its duration instead of stacking; distinct
        // assets that share a StatusEffectType (e.g. two different ItemPassive
        // effects from two items) coexist. Keyed on Data identity, not type —
        // keying on type made unrelated item passives silently swallow each
        // other. This deliberately does not fire OnStatusEffectRemoved.
        StatusEffectInstance existing = null;
        foreach (var statusEffect in statusEffects)
        {
            if (statusEffect.Data == statusEffectInstance.Data)
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

        // A status may carry its own stat modifier (StatusEffectData.modifier*),
        // evaluated once against the host at apply time and sourced to the
        // INSTANCE — RemoveStatusEffect strips it again, so timed buffs like
        // Enraged ("double attack for 2 turns") clean up on expiry, dispel, or
        // item sweep alike. Only on a fresh add: a refresh must not stack it.
        if (statusEffectInstance.Data.modifierHealth != null || statusEffectInstance.Data.modifierAttack != null)
        {
            var modifierContext = new EffectContext(this,
                new GameEvent(GameEventType.OnStatusEffectApplied, this,
                    new StatusEffectEventData(statusEffectInstance)), statusEffectInstance);
            int hp = statusEffectInstance.Data.modifierHealth?.CalculateValue(modifierContext) ?? 0;
            int atk = statusEffectInstance.Data.modifierAttack?.CalculateValue(modifierContext) ?? 0;
            AddModifier(new StatModifier(statusEffectInstance, hp, atk));
        }

        await HandleEvent(new GameEvent(GameEventType.OnStatusEffectApplied, this,
            new StatusEffectEventData(statusEffectInstance)));
        OnStatusEffectAdded?.Invoke(statusEffectInstance);
    }

    public async Task RemoveStatusEffect(StatusEffectData statusEffectData)
    {
        // Match by asset identity, not type: distinct assets can share a
        // StatusEffectType (e.g. two ItemPassive effects from two items), and
        // removing "by type" could strip the other item's passive.
        StatusEffectInstance toRemove = null;
        foreach (var statusEffect in statusEffects)
        {
            if (statusEffect.Data == statusEffectData)
            {
                toRemove = statusEffect;
                break;
            }
        }

        await RemoveStatusEffect(toRemove);
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
            // Modifiers sourced by a status instance die with it (the apply-side
            // counterpart lives in ApplyStatusEffect).
            RemoveModifiersFrom(statusEffectInstance);

            await HandleEvent(new GameEvent(GameEventType.OnStatusEffectRemoved, this,
                new StatusEffectEventData(statusEffectInstance)));
            OnStatusEffectRemoved?.Invoke(statusEffectInstance);
        }
    }

    // Removes every status this SOURCE card applied to this minion. Equipment
    // semantics for items, mirroring Board.RemoveModifiersGrantedBy: "holder can
    // only be damaged once per round" must not outlive its lantern. Called by
    // Portal.RemoveCard for the leaving item's holder — deliberately NOT for
    // statuses the item put on other minions (Hidden Grenade's death-stun is
    // applied by the same cascade that removes the grenade and must survive it).
    public async Task RemoveStatusEffectsFrom(CardInstance source)
    {
        foreach (var statusEffect in statusEffects.ToList())
        {
            if (ReferenceEquals(statusEffect.Source, source))
            {
                await RemoveStatusEffect(statusEffect);
            }
        }
    }

    // A scripted kill (sacrifice): no damage event, so shields don't soak it and
    // no OnDamaged fires — health drops to zero and the death goes through the
    // normal batch (OnKilled deathrattles fire, the killer is credited).
    public void KillOutright(CardInstance killer)
    {
        if (!IsAlive) return;
        CurrentHealth = 0;
        OnStatsChanged?.Invoke(CurrentHealth, CurrentAttack);
        Board.ReportDeath(this, killer);
    }

    public bool HasStatusEffect(StatusEffectType statusEffectType)
    {
        foreach (StatusEffectInstance se in statusEffects)
        {
            if (se.Data.effectName == statusEffectType) return true;
        }

        return false;
    }

    public override string ToString()
    {
        string status = statusEffects.Count > 0
            ? ", " + string.Join("+", statusEffects.ConvertAll(s => s.Data.effectName.ToString()))
            : "";
        return $"{base.ToString()} [HP {CurrentHealth}/{MaxHealth}, ATK {CurrentAttack}, SHIELD {Shield}{status}]";
    }
}

public class SpellInstance : FieldableCardInstance, IGameEventReceiver
{
    private SpellType Definition;

    public override async Task HandleEvent(GameEvent evt)
    {
        await PlayEventAudioAndDelayOnPlayed(evt);
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
        await PlayEventAudioAndDelayOnPlayed(evt);
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
