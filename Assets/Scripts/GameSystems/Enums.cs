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
    None,
    Plague,
    Burn,
    Freeze,
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