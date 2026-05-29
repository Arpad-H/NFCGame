using UnityEngine;

[System.Serializable]
public class AudioOnEvent
{
   // [SerializeReference]
    public GameEventType Trigger;
    [SerializeReference] 
    public AudioClip AudioClip;
    
    public void TryPlayAudio(GameEvent evt)
    {
        if (evt.GetEventType() == Trigger)
        {
           // AudioManager.Instance.PlaySound(AudioClip);
        }
    }
}