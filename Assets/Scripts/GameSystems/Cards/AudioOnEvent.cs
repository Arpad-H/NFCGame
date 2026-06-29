using UnityEngine;

[System.Serializable]
public class AudioOnEvent
{

    public GameEventType Trigger;
    public AudioClip AudioClip;
    
    // Returns the played clip's length in seconds (0 when nothing played) so
    // callers can wait for the sound to finish before continuing.
    public float TryPlayAudio(GameEvent evt)
    {
        if (evt.GetEventType() == Trigger && AudioClip != null)
        {
           AudioManager.Instance.PlaySound(AudioClip);
           return AudioClip.length;
        }

        return 0f;
    }
}