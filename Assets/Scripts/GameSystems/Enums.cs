using System;
using System.Collections.Generic;

namespace GameSystems
{
    public enum PlayerSide { Left, Right }

    public enum Rune
    {
        None,
        Diamond,
        Lightning,
        Fire,
        Water,
        Death,
        Plague,
        Spirit,
        Life,
        Gravity,
        Sword,
    }

public enum MinionStats
{
    Health,
    Attack,
}

public enum StatusEffectType
{
    Plague = 0,
    Burn = 1,
    Freeze = 2,
    ItemPassive = 3,
    Hidden = 4,
    Stun = 5,    // skips the unit's combat action while present
    Sleep = 6,   // like Stun, but removed when the unit takes damage
    Stealth = 7, // untargetable by default attack targeting / ExcludeStatusEffects
}

[System.Flags]
public enum StatusEffectMask
{
    Plague = 1 << 0, // 1
    Burn = 1 << 1, // 2
    Freeze = 1 << 2, // 4
    ItemPassive = 1 << 3,
    Hidden = 1 << 4,
    Stun = 1 << 5,
    Sleep = 1 << 6,
    Stealth = 1 << 7,
    All = ~0 // -1
}

// What kind of action produced a damage event. Lets triggers and conditions
// distinguish minion attacks from spell/effect damage (e.g. "blocks all damage
// that does not originate from minion attacks", lifesteal on attack only).
public enum DamageSourceType
{
    Attack,       // a minion's combat attack (Default/TriggerAttack effects)
    Effect,       // a card effect (DamageEffect from minion/item triggers)
    Spell,        // a spell card's effect
    StatusEffect, // periodic status damage (burn, plague ticks)
}

public static class StatusEffectExtension
{
    public static List<StatusEffectType> MaskToTypes(StatusEffectMask mask)
    {
        List<StatusEffectType> result = new();

        foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
        {
            StatusEffectMask flag = (StatusEffectMask)(1 << (int)type);

            if ((mask & flag) != 0)
            {
                result.Add(type);
            }
        }

        return result;
    }
}

public enum EffectFieldPosition
{
    OnCombatResolveEffect,
    Passive,
    Effect1,
    Effect2,
    StatusEffect,
}

  //  public enum TargetType {Default, OwnPlayer, EnemyPlayer, AllMinions, OwnMinions, EnemyMinions, SpecificMinion }
  //public enum CardType { Minion, Spell, Enchantment, Hero }
}