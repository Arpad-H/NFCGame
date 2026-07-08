using System.Collections.Generic;
using UnityEngine;

// Per-clip playback gains, measured once in the editor by the Loudness Baker
// (Tools > Audio > Loudness Baker) so that every SFX is heard at the same
// perceived loudness without re-exporting the source files.
[CreateAssetMenu(menuName = "Audio/Loudness Table", fileName = "AudioLoudnessTable")]
public class AudioLoudnessTable : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public AudioClip Clip;
        public float Gain;            // Linear multiplier, fed to PlayOneShot's volumeScale.
        public float MeasuredLufs;    // What the clip measured before the gain was applied.
        public float PlaybackLength;  // Seconds of audible signal; trailing silence excluded.
    }

    [Tooltip("Loudness every clip is matched to, in LUFS. -18..-14 suits one-shot game SFX; " +
             "lower leaves more headroom for overlapping sounds.")]
    public float TargetLufs = -16f;

    [Tooltip("A clip's peak is never pushed above this after gain (dBFS), so a normalised " +
             "clip can't clip the mix on its own.")]
    public float PeakCeilingDb = -1f;

    [Tooltip("Gain is never boosted past this, so near-silent clips don't get their noise floor amplified.")]
    public float MaxBoostDb = 18f;

    [Tooltip("Added to the gain of mono clips only. BS.1770 measures a mono clip 3 LU quieter than " +
             "the same signal as dual-mono stereo, but Unity plays both the same way. Set to -3 if " +
             "your mono SFX come out hot next to the stereo ones.")]
    public float MonoCompensationDb = 0f;

    [Tooltip("Trailing audio quieter than this (relative to the clip's own peak) doesn't count " +
             "towards PlaybackLength.")]
    public float SilenceFloorDb = -40f;

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<AudioClip, Entry> lookup;

    public IReadOnlyList<Entry> Entries => entries;

    // Dropped on domain reload and after a re-bake, then rebuilt on first access.
    private void OnEnable() => lookup = null;

    private Dictionary<AudioClip, Entry> Lookup
    {
        get
        {
            if (lookup != null) return lookup;

            lookup = new Dictionary<AudioClip, Entry>(entries.Count);
            foreach (Entry entry in entries)
            {
                if (entry.Clip != null) lookup[entry.Clip] = entry;
            }

            return lookup;
        }
    }

    // 1 for clips that were never analysed, so a partially baked table is harmless.
    public float GetGain(AudioClip clip)
        => clip != null && Lookup.TryGetValue(clip, out Entry entry) ? entry.Gain : 1f;

    // How long the clip is actually audible for. Callers that stall gameplay until a
    // sound finishes should wait this long rather than clip.length, which counts the
    // silent tail baked into a lot of exported SFX.
    public float GetPlaybackLength(AudioClip clip)
    {
        if (clip == null) return 0f;

        return Lookup.TryGetValue(clip, out Entry entry) && entry.PlaybackLength > 0f
            ? entry.PlaybackLength
            : clip.length;
    }

#if UNITY_EDITOR
    public void SetEntries(List<Entry> baked)
    {
        entries = baked;
        lookup = null;
    }
#endif
}
