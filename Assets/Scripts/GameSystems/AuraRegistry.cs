using System.Collections.Generic;
using System.Linq;
using GameSystems.Cards;

// A continuous "while X is on the field" effect. Target logic and amounts are
// re-resolved on every Reevaluate(), so auras automatically cover minions
// played after the source, positional targets that shift when lanes change,
// and dynamic amounts like "+1 HP per rat in play".
public class ActiveAura
{
    public FieldableCardInstance Source;
    public ITargetLogic TargetLogic;
    public ICalculateValueLogic HealthAmount;
    public ICalculateValueLogic AttackAmount;

    // Modifiers this aura currently has applied, per minion. Used by
    // Reevaluate() to diff desired vs. applied state.
    public readonly Dictionary<MinionInstance, StatModifier> Applied = new();
}

// Owned by Board. Holds all active auras and keeps their stat modifiers in
// sync with the board state. Reevaluate() is idempotent and delta-based:
// refreshing an unchanged aura never re-heals damage.
public class AuraRegistry
{
    private readonly List<ActiveAura> auras = new();

    public void Register(ActiveAura aura)
    {
        auras.Add(aura);
        Reevaluate();
    }

    // Removes every aura granted by this source and detaches their modifiers.
    public void UnregisterAllFrom(FieldableCardInstance source)
    {
        foreach (var aura in auras.Where(a => a.Source == source).ToList())
        {
            foreach (var (minion, modifier) in aura.Applied.ToList())
            {
                minion.RemoveModifier(modifier);
            }

            auras.Remove(aura);
        }
    }

    public void Reevaluate()
    {
        foreach (var aura in auras.ToList())
        {
            var context = new EffectContext(aura.Source, default);
            var desired = new HashSet<MinionInstance>();

            foreach (var target in aura.TargetLogic.GetTargets(context))
            {
                if (target is MinionInstance minion && minion.IsAlive) desired.Add(minion);
            }

            int health = aura.HealthAmount?.CalculateValue(context) ?? 0;
            int attack = aura.AttackAmount?.CalculateValue(context) ?? 0;

            // Drop minions that left the target set (died, moved, lost status).
            foreach (var (minion, modifier) in aura.Applied.ToList())
            {
                if (desired.Contains(minion)) continue;
                minion.RemoveModifier(modifier);
                aura.Applied.Remove(minion);
            }

            foreach (var minion in desired)
            {
                if (aura.Applied.TryGetValue(minion, out var existing))
                {
                    // Amount changed (e.g. a rat died): adjust by the delta only.
                    if (existing.Health != health || existing.Attack != attack)
                    {
                        minion.AdjustModifier(existing, health, attack);
                    }
                }
                else
                {
                    var modifier = new StatModifier(aura, health, attack);
                    aura.Applied[minion] = modifier;
                    minion.AddModifier(modifier);
                }
            }
        }
    }
}
