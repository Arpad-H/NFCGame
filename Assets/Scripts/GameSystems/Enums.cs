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
}

[System.Flags]
public enum StatusEffectsToTriggerOn
{
    Plague = 1 << 0, // 1
    Burn = 1 << 1, // 2
    Freeze = 1 << 2, // 4
    All = ~0 // -1
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