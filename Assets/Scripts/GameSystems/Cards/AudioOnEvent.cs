using UnityEngine;

[System.Serializable]
public class AudioOnEvent
{

    public GameEventType Trigger;
    public AudioClip AudioClip;
    
    public void TryPlayAudio(GameEvent evt)
    {
        if (evt.GetEventType() == Trigger)
        {
           AudioManager.Instance.PlaySound(AudioClip);
        }
    }
}