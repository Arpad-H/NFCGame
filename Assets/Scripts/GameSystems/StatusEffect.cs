// StatusEffect.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using GameSystems;
using UnityEngine;

[CreateAssetMenu(fileName = "Status Effect", menuName = "Status Effect")]
public class StatusEffectData : ScriptableObject
{
    public StatusEffectType effectName;
    public Sprite icon;
    public GameObject vfxPrefab;
    
    // Put your modular lego-brick triggers directly into the SO
    [SerializeReference] [SubclassSelector]
    public List<IEventTrigger> triggers = new();
}

public class StatusEffectInstance
{
    public StatusEffectData Data { get; private set; }
    public int DurationRemaining { get; set; }
    
    public StatusEffectInstance(StatusEffectData data, int duration)
    {
        Data = data;
        DurationRemaining = duration;
    }

    public async Task HandleEvent(GameEvent evt, MinionInstance hostMinion)
    {
        // Execute the modular triggers
        foreach (var trigger in Data.triggers)
        {
          
            
            var binding = new TriggerBinding 
            { 
                Trigger = trigger, 
                EffectIndex = EffectFieldPosition.StatusEffect // Or define a specific enum for this
            };

            if (trigger.CanTrigger(evt, binding))
            {
                // The context 'Instance' is the minion hosting the status effect
                await trigger.Execute(new EffectContext(hostMinion, evt));
            }
        }
        // Auto-decrement duration on round end
        if (evt.Type == GameEventType.OnRoundEnd)
        {
            DurationRemaining--;
            if (DurationRemaining <= 0)
            {
                // Note: Ensure MinionInstance exposes a RemoveStatusEffect(this) method
                hostMinion.RemoveStatusEffect(this);
                return; 
            }
        }
    }
}