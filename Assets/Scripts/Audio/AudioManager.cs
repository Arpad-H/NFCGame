using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    public TraumaInducer traumaInducer;
    public AdaptiveAudioTrack adaptiveAudioTrack;
    [SerializeField] private AudioSource winAudioSource;
    [SerializeField] private AudioSource lossAudioSource;
    [SerializeField] private AudioSource invalidActionAudioSource;
    [SerializeField] private AudioSource playerActionSuccessAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioSource minionClashAudioSource;

    [Tooltip("Per-clip gains baked by Tools > Audio > Loudness Baker, so clips of differing " +
             "levels play back equally loud. Falls back to Resources/AudioLoudnessTable, and " +
             "to no gain at all when neither is present.")]
    [SerializeField] private AudioLoudnessTable loudnessTable;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (sfxAudioSource == null)
            sfxAudioSource = gameObject.AddComponent<AudioSource>();

        if (loudnessTable == null)
            loudnessTable = Resources.Load<AudioLoudnessTable>("AudioLoudnessTable");

        // The one-shot sources below play a fixed clip via Play() rather than through
        // PlaySound, so their gain has to be folded into the source's volume up front.
        NormalizeSource(winAudioSource);
        NormalizeSource(lossAudioSource);
        NormalizeSource(invalidActionAudioSource);
        NormalizeSource(playerActionSuccessAudioSource);
        NormalizeSource(minionClashAudioSource);
    }

    // Scales the source's authored volume by its clip's baked gain, preserving any
    // level the designer set by hand. Clips missing from the table are left alone.
    private void NormalizeSource(AudioSource source)
    {
        if (source == null || source.clip == null) return;

        source.volume *= GetGain(source.clip);
    }

    // Baked playback gain for a clip, or 1 when it hasn't been analysed.
    public float GetGain(AudioClip audioClip)
        => loudnessTable != null ? loudnessTable.GetGain(audioClip) : 1f;

    // How long the clip is audible for, ignoring any silent tail. Callers that hold up
    // gameplay until a sound finishes should wait this long instead of clip.length.
    public float GetPlaybackLength(AudioClip audioClip)
    {
        if (audioClip == null) return 0f;

        return loudnessTable != null ? loudnessTable.GetPlaybackLength(audioClip) : audioClip.length;
    }
    void Start()
    {
        PlayBackgroundMusic();
    }
    
    public void PlayWinAudio()
    {
        winAudioSource.Play();
    }
    public void PlayLossAudio()
    {
        lossAudioSource.Play();
    }
    public void PlayBackgroundMusic()
    {
        adaptiveAudioTrack.Play();
    }
    public void StopBackgroundMusic() 
    {
        adaptiveAudioTrack.Stop();
    }
    public void ToggleAdaptiveLayer(int intensity, bool enable)
    {
        if (enable)
            adaptiveAudioTrack.ToggleState(intensity, AdaptiveAudioTrack.LayerState.UNMUTED);
        else adaptiveAudioTrack.ToggleState(intensity, AdaptiveAudioTrack.LayerState.MUTED);
    }
    public void PlayInvalidActionSound()
    {
        invalidActionAudioSource.Play();
    }
    public void PlayPlayerActionSuccessSound()
    {
        playerActionSuccessAudioSource.Play();
    }

    public void PlaySound(AudioClip audioClip, float volumeScale = 1f)
    {
        if (audioClip == null)
            return;

        sfxAudioSource.PlayOneShot(audioClip, volumeScale * GetGain(audioClip));
    }
    public void PlayMinionClashSound()
    {
        minionClashAudioSource.Play();
        traumaInducer.ShakeCamera();
    }
}