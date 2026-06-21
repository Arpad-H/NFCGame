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

    public void PlaySound(AudioClip audioClip)
    {
        if (audioClip == null)
            return;

        sfxAudioSource.PlayOneShot(audioClip);
    }
    public void PlayMinionClashSound()
    {
        minionClashAudioSource.Play();
        traumaInducer.ShakeCamera();
    }
}