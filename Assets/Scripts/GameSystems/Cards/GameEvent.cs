using System.Collections.Generic;
using GameSystems;

// ── Payload contract ──────────────────────────────────────────────────────────
//
//  GameEventType              → GameEventData subclass  (null = no payload)
//  ─────────────────────────────────────────────────────
//  OnRoundStart               → RoundEventData
//  OnRoundEnd                 → null
//  OnCombatResolution         → null  (not a broadcast: delivered to each
//                                      attacking minion as its blow lands)
//  OnPlayed                   → null          
//  OnAboutToAttack            → AttackEventData
//  OnAboutToTakeDamage        → DamageEventData   (mutable: IsPrevented)
//  OnAboutToBeHealed          → HealEventData     (mutable: IsPrevented)
//  OnDamaged                  → SourceEventData
//  OnKilled                   → SourceEventData
//  OnHealed                   → SourceEventData
//  OnAttack                   → AttackEventData
//  OnCardDrawn                → PlayerEventData
//  OnCardDiscarded            → PlayerEventData
//  OnActivateEffectEvent      → EffectFieldEventData
//  OnDeactivateEffectEvent    → EffectFieldEventData  (was wrongly int, now fixed)
//  OnStatusEffectApplied      → StatusEffectEventData
//  OnStatusEffectRemoved      → StatusEffectEventData

public abstract class GameEventData { }

// OnRoundStart
public class RoundEventData : GameEventData
{
    public readonly int Round;
    public RoundEventData(int round) { Round = round; }
}

// OnAboutToTakeDamage — mutable so interception handlers can set IsPrevented
public class DamageEventData : GameEventData
{
    public int Amount;
    public CardInstance Source;
    public bool IsPrevented;

    // What produced this damage (attack vs. effect vs. spell vs. status tick).
    public DamageSourceType SourceType;

    // One of the two simultaneous blows of a lane clash, where the minions have
    // met in the middle and overlap. Presentation only: the resolver plays a
    // single impact cue for the collision (so these hits play none), and the
    // damage numbers are pushed apart instead of stacking on the meeting point.
    public bool IsClashHit;

    public DamageEventData(int amount, CardInstance source = null,
        DamageSourceType sourceType = DamageSourceType.Effect)
    {
        Amount = amount;
        Source = source;
        SourceType = sourceType;
        IsPrevented = false;
    }
}

// OnAboutToBeHealed — mutable so interception handlers can set IsPrevented
public class HealEventData : GameEventData
{
    public int Amount;
    public CardInstance Source;
    public bool IsPrevented;

    public HealEventData(int amount, CardInstance source = null)
    {
        Amount = amount;
        Source = source;
        IsPrevented = false;
    }
}

// OnCardDrawn, OnCardDiscarded
public class PlayerEventData : GameEventData
{
    public readonly Player Player;
    public PlayerEventData(Player player) { Player = player; }
}

// OnDamaged, OnKilled, OnHealed — the entity that caused the event and the
// amount involved (damage dealt / healing received; 0 where not applicable).
public class SourceEventData : GameEventData
{
    public readonly CardInstance Source;
    public readonly int Amount;

    public SourceEventData(CardInstance source, int amount = 0)
    {
        Source = source;
        Amount = amount;
    }
}

// OnAttack
public class AttackEventData : GameEventData
{
    public readonly List<ITargetable> Targets;
    public AttackEventData(List<ITargetable> targets) { Targets = targets; }
}

// OnActivateEffectEvent, OnDeactivateEffectEvent
public class EffectFieldEventData : GameEventData
{
    public readonly EffectFieldPosition Position;
    public EffectFieldEventData(EffectFieldPosition position) { Position = position; }
}

// OnStatusEffectApplied, OnStatusEffectRemoved
public class StatusEffectEventData : GameEventData
{
    public readonly StatusEffectInstance StatusEffect;
    public StatusEffectEventData(StatusEffectInstance statusEffect) { StatusEffect = statusEffect; }
}

// ── Event type enumeration ────────────────────────────────────────────────────

public enum GameEventType
{
    OnPlayed,
    OnRoundStart,
    OnRoundEnd,
    OnAboutToAttack,
    OnAttack,
    OnAboutToTakeDamage,
    OnDamaged,
    OnAboutToBeHealed,
    OnHealed,
    OnKilled,
    OnCombatResolution,
    OnCardDrawn,
    OnCardDiscarded,
    OnActivateEffectEvent,
    OnDeactivateEffectEvent,
    OnStatusEffectApplied,
    OnStatusEffectRemoved,
}

// ── Event envelope ────────────────────────────────────────────────────────────

public readonly struct GameEvent
{
    public readonly GameEventType Type;
    public readonly FieldableCardInstance EffectSource;
    public readonly GameEventData GameEventPayload;

    public GameEvent(GameEventType type, FieldableCardInstance instance, GameEventData gameEventPayload = null)
    {
        Type = type;
        EffectSource = instance;
        GameEventPayload = gameEventPayload;
    }

    public GameEventType GetEventType() => Type;
}
