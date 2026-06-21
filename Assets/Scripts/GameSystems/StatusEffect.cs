// StatusEffect.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using GameSystems;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "Status Effect", menuName = "Status Effect")]
public class StatusEffectData : ScriptableObject
{
    public StatusEffectType effectName;
    public Sprite icon;
    public GameObject vfxPrefab;
    
    [SerializeReference] [SubclassSelector]
    public List<IEventTrigger> triggers = new();
}

public class StatusEffectInstance
{
    public StatusEffectData Data { get; private set; }
    public int DurationRemaining { get; set; }

    // The card that applied this effect (attacker, plague carrier, ...). Lets
    // triggers branch on whether the applier was friendly or hostile to the
    // host minion. May be null for effects applied without a clear source.
    public CardInstance Source { get; set; }

    // Built once per instance so triggers can keep per-instance state in
    // StateSlot; the trigger SOs themselves stay stateless.
    private readonly List<TriggerBinding> bindings;

    public StatusEffectInstance(StatusEffectData data, int duration, CardInstance source = null)
    {
        Data = data;
        DurationRemaining = duration;
        Source = source;
        bindings = new List<TriggerBinding>();
        foreach (var trigger in data.triggers)
        {
            bindings.Add(new TriggerBinding(trigger, EffectFieldPosition.StatusEffect));
        }
    }

    public async Task HandleEvent(GameEvent evt, MinionInstance hostMinion)
    {
        foreach (var binding in bindings)
        {
            if (binding.Trigger.CanTrigger(evt, binding))
            {
                // The context 'Instance' is the minion hosting the status effect.
                // Passing 'this' lets triggers reach the effect's Source.
                await binding.Trigger.Execute(new EffectContext(hostMinion, evt, this));
            }
        }
        // Auto-decrement duration on round end
        if (evt.Type == GameEventType.OnRoundEnd)
        {
            DurationRemaining--;
            if (DurationRemaining <= 0)
            {
                // Note: Ensure MinionInstance exposes a RemoveStatusEffect(this) method
                await hostMinion.RemoveStatusEffect(this);
            }
        }
    }
}