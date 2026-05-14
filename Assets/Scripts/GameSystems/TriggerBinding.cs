using GameSystems;

public class TriggerBinding
{
    public TriggerBinding(){}
    public TriggerBinding(IEventTrigger trigger,EffectFieldPosition effectIndex)
    {
        Trigger = trigger;
        EffectIndex = effectIndex;
    }
    public IEventTrigger Trigger { get; set; }
    public EffectFieldPosition EffectIndex { get; set; }
}