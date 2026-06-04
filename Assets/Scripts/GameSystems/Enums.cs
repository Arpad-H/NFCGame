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
    Hidden = 4
}

[System.Flags]
public enum StatusEffectMask
{
    Plague = 1 << 0, // 1
    Burn = 1 << 1, // 2
    Freeze = 1 << 2, // 4
    ItemPassive = 1 << 3,
    Hidden = 1 << 4,
    All = ~0 // -1
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