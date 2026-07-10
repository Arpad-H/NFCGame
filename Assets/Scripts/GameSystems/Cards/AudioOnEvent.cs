using UnityEngine;

[System.Serializable]
public class AudioOnEvent
{

    public GameEventType Trigger;
    public AudioClip AudioClip;

    // Returns the played clip's audible length in seconds (0 when nothing played) so
    // callers can wait for the sound to finish before continuing. This is the clip's
    // length minus any trailing silence, per the baked loudness table — waiting on the
    // raw clip.length would stall the turn through silence the player never hears.
    public float TryPlayAudio(GameEvent evt)
    {
        if (evt.GetEventType() == Trigger && AudioClip != null && AudioManager.Instance != null)
        {
           AudioManager.Instance.PlaySound(AudioClip);
           return AudioManager.Instance.GetPlaybackLength(AudioClip);
        }

        return 0f;
    }
}
