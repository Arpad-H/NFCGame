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

    // Set on damage produced by MirrorPortalDamageEffect (Cepter of Osiris).
    // Mirrored damage is never mirrored again, so two Cepters can't ping-pong
    // portal damage forever.
    public bool IsMirroredPortalDamage;

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

// OnPortalDamaged — broadcast by Portal.TakeDamage after the hit lands.
// EffectSource is null (a portal is not a card); listeners identify the portal
// through this payload. Amount is the damage actually applied (after the
// portal's damage multiplier), Original the damage event that caused it.
public class PortalDamagedEventData : GameEventData
{
    public readonly Portal Portal;
    public readonly int Amount;
    public readonly DamageEventData Original;

    public PortalDamagedEventData(Portal portal, int amount, DamageEventData original)
    {
        Portal = portal;
        Amount = amount;
        Original = original;
    }
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
    OnPortalDamaged, // append-only: enum indices are serialized in card assets
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

    // Every diagnostic in the event pipeline (drain trace, runaway-loop dump,
    // effect logs) formats events through here, so an event always identifies
    // WHO it happened to and WHAT caused it — not just its type.
    public override string ToString()
    {
        string payload = DescribePayload();
        string head = $"{Type}@{Describe(EffectSource)}";
        return payload == null ? head : $"{head} ({payload})";
    }

    private string DescribePayload()
    {
        switch (GameEventPayload)
        {
            case null: return null;
            case RoundEventData r: return $"round {r.Round}";
            case DamageEventData d:
                return $"{d.Amount} {d.SourceType} dmg from {Describe(d.Source)}" +
                       (d.IsPrevented ? ", PREVENTED" : "") + (d.IsClashHit ? ", clash" : "");
            case HealEventData h:
                return $"{h.Amount} heal from {Describe(h.Source)}" + (h.IsPrevented ? ", PREVENTED" : "");
            case SourceEventData s: return $"amount {s.Amount} from {Describe(s.Source)}";
            case PlayerEventData p: return Describe(p.Player);
            case AttackEventData a:
                return a.Targets == null || a.Targets.Count == 0
                    ? "no targets"
                    : "targets " + string.Join(" + ", a.Targets);
            case EffectFieldEventData e: return $"field {e.Position}";
            case StatusEffectEventData s:
                return $"status {s.StatusEffect.Data.effectName} from {Describe(s.StatusEffect.Source)}";
            case PortalDamagedEventData p:
                return $"{p.Amount} dmg to {Describe(p.Portal)}";
            default: return GameEventPayload.GetType().Name;
        }
    }

    // Null-safe: a sourceless effect ("nothing") is itself a useful clue when
    // chasing a loop, and must never turn a diagnostic log into a NullReference.
    public static string Describe(object entity) => entity?.ToString() ?? "nothing";
}
