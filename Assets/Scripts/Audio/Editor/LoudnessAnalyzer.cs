using System;
using UnityEngine;

// Perceived-loudness measurement following ITU-R BS.1770-4, the same standard
// broadcast and streaming use for loudness matching.
//
// Peak or plain RMS both mismeasure SFX: two clips can share a peak while one
// sounds twice as loud, and a bright, harsh clip reads quieter under RMS than
// it sounds. BS.1770 fixes both by running the signal through a "K-weighting"
// filter (a high shelf plus a low cut, roughly the ear's frequency response)
// before taking mean square power, then gating out the quiet parts so a long
// silent tail can't drag the number down.
public static class LoudnessAnalyzer
{
    public enum Metric
    {
        // Gated average over the whole clip. Matches how loud the clip feels overall.
        Integrated,

        // Loudest 400 ms window. Matches how loud the clip's transient hits, which is
        // often what you want for short impact SFX.
        MaxMomentary,
    }

    public struct Result
    {
        public bool Valid;
        public string Error;
        public float Lufs;           // Measured loudness.
        public float Peak;           // Highest absolute sample value, 0..1.
        public float AudibleLength;  // Seconds until the tail drops below the silence floor.
    }

    private const double AbsoluteGateLufs = -70.0;  // BS.1770 absolute gate.
    private const double RelativeGateLu = -10.0;    // BS.1770 relative gate, below the ungated mean.
    private const double LoudnessOffset = -0.691;   // Calibration constant from the spec.

    private const double BlockSeconds = 0.4;   // Momentary window.
    private const double StepSeconds = 0.1;    // 75% overlap between windows.

    private const float TailPadSeconds = 0.05f;

    public static Result Analyze(AudioClip clip, Metric metric, float silenceFloorDb)
    {
        if (clip == null) return Fail("clip is null");

        int channels = clip.channels;
        int frames = clip.samples;
        int rate = clip.frequency;

        if (channels <= 0 || frames <= 0 || rate <= 0) return Fail("clip has no samples");

        float[] interleaved = new float[frames * channels];
        if (!clip.GetData(interleaved, 0))
        {
            return Fail("GetData failed — the clip's load type must be Decompress On Load to be read in the editor");
        }

        float peak = MeasurePeak(interleaved);
        if (peak <= 0f)
        {
            // Streaming/compressed clips, and clips whose platform-specific import override
            // wins over the default settings, hand back a buffer of zeros rather than erroring.
            return Fail("all samples are silent — check the clip's platform-specific import override");
        }

        double[][] weighted = Deinterleave(interleaved, frames, channels);
        foreach (double[] channel in weighted)
        {
            ApplyKWeighting(channel, rate);
        }

        double lufs = metric == Metric.MaxMomentary
            ? MaxMomentaryLoudness(weighted, frames, channels, rate)
            : IntegratedLoudness(weighted, frames, channels, rate);

        if (double.IsNegativeInfinity(lufs) || double.IsNaN(lufs)) return Fail("loudness measured as silence");

        return new Result
        {
            Valid = true,
            Lufs = (float)lufs,
            Peak = peak,
            AudibleLength = MeasureAudibleLength(interleaved, frames, channels, rate, peak, silenceFloorDb),
        };
    }

    private static Result Fail(string error) => new Result { Valid = false, Error = error };

    private static float MeasurePeak(float[] interleaved)
    {
        float peak = 0f;
        for (int i = 0; i < interleaved.Length; i++)
        {
            float magnitude = Mathf.Abs(interleaved[i]);
            if (magnitude > peak) peak = magnitude;
        }

        return peak;
    }

    // Walks back from the end of the clip to the last frame louder than the silence
    // floor. Exported SFX routinely carry a second or more of digital silence, and
    // anything that waits on clip.length pays for it.
    private static float MeasureAudibleLength(float[] interleaved, int frames, int channels,
                                              int rate, float peak, float silenceFloorDb)
    {
        float threshold = peak * Mathf.Pow(10f, silenceFloorDb / 20f);

        for (int frame = frames - 1; frame >= 0; frame--)
        {
            int baseIndex = frame * channels;
            for (int channel = 0; channel < channels; channel++)
            {
                if (Mathf.Abs(interleaved[baseIndex + channel]) < threshold) continue;

                float seconds = (frame + 1) / (float)rate + TailPadSeconds;
                return Mathf.Min(seconds, frames / (float)rate);
            }
        }

        return 0f;
    }

    private static double[][] Deinterleave(float[] interleaved, int frames, int channels)
    {
        double[][] result = new double[channels][];
        for (int channel = 0; channel < channels; channel++)
        {
            double[] samples = new double[frames];
            for (int frame = 0; frame < frames; frame++)
            {
                samples[frame] = interleaved[frame * channels + channel];
            }

            result[channel] = samples;
        }

        return result;
    }

    // BS.1770 channel weights: L, R and C count once; the surrounds count for more
    // because sound arriving from behind is perceived as louder. Mono and stereo,
    // which is all a game's SFX bank realistically holds, come out at weight 1.
    private static double ChannelWeight(int channel, int channels)
    {
        if (channels < 5) return 1.0;      // Mono / stereo / quad: no surround weighting.
        return channel == 3 || channel == 4 ? 1.41 : 1.0;
    }

    // K-weighting: a +4 dB high shelf standing in for the head's diffraction of
    // high frequencies, then a low cut discarding rumble the ear barely registers.
    private static void ApplyKWeighting(double[] samples, int rate)
    {
        // Stage 1 — high shelf.
        {
            const double f0 = 1681.974450955533;
            const double gainDb = 3.999843853973347;
            const double q = 0.7071752369554196;

            double k = Math.Tan(Math.PI * f0 / rate);
            double vh = Math.Pow(10.0, gainDb / 20.0);
            double vb = Math.Pow(vh, 0.4996667741545416);
            double a0 = 1.0 + k / q + k * k;

            Biquad(samples,
                b0: (vh + vb * k / q + k * k) / a0,
                b1: 2.0 * (k * k - vh) / a0,
                b2: (vh - vb * k / q + k * k) / a0,
                a1: 2.0 * (k * k - 1.0) / a0,
                a2: (1.0 - k / q + k * k) / a0);
        }

        // Stage 2 — RLB high-pass.
        {
            const double f0 = 38.13547087602444;
            const double q = 0.5003270373238773;

            double k = Math.Tan(Math.PI * f0 / rate);
            double denominator = 1.0 + k / q + k * k;

            Biquad(samples,
                b0: 1.0,
                b1: -2.0,
                b2: 1.0,
                a1: 2.0 * (k * k - 1.0) / denominator,
                a2: (1.0 - k / q + k * k) / denominator);
        }
    }

    private static void Biquad(double[] samples, double b0, double b1, double b2, double a1, double a2)
    {
        double x1 = 0.0, x2 = 0.0, y1 = 0.0, y2 = 0.0;

        for (int i = 0; i < samples.Length; i++)
        {
            double x0 = samples[i];
            double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;

            x2 = x1; x1 = x0;
            y2 = y1; y1 = y0;

            samples[i] = y0;
        }
    }

    // Mean square power of one 400 ms window, per channel.
    private static void BlockPower(double[][] weighted, int start, int blockFrames, double[] power)
    {
        for (int channel = 0; channel < weighted.Length; channel++)
        {
            double sum = 0.0;
            double[] samples = weighted[channel];
            for (int i = start; i < start + blockFrames; i++)
            {
                sum += samples[i] * samples[i];
            }

            power[channel] = sum / blockFrames;
        }
    }

    private static double LoudnessOf(double[] power, int channels)
    {
        double sum = 0.0;
        for (int channel = 0; channel < channels; channel++)
        {
            sum += ChannelWeight(channel, channels) * power[channel];
        }

        return sum <= 0.0 ? double.NegativeInfinity : LoudnessOffset + 10.0 * Math.Log10(sum);
    }

    // Collects the per-channel power of every overlapping 400 ms window. Clips shorter
    // than one window — most impact SFX are — get measured as a single whole-clip window
    // instead, which is ungated but has nothing to gate anyway.
    private static double[][] CollectBlocks(double[][] weighted, int frames, int channels, int rate)
    {
        int blockFrames = (int)Math.Round(BlockSeconds * rate);
        int stepFrames = Math.Max(1, (int)Math.Round(StepSeconds * rate));

        if (frames < blockFrames)
        {
            double[] single = new double[channels];
            BlockPower(weighted, 0, frames, single);
            return new[] { single };
        }

        int blockCount = (frames - blockFrames) / stepFrames + 1;
        double[][] blocks = new double[blockCount][];

        for (int block = 0; block < blockCount; block++)
        {
            double[] power = new double[channels];
            BlockPower(weighted, block * stepFrames, blockFrames, power);
            blocks[block] = power;
        }

        return blocks;
    }

    private static double MaxMomentaryLoudness(double[][] weighted, int frames, int channels, int rate)
    {
        double loudest = double.NegativeInfinity;
        foreach (double[] block in CollectBlocks(weighted, frames, channels, rate))
        {
            loudest = Math.Max(loudest, LoudnessOf(block, channels));
        }

        return loudest;
    }

    private static double IntegratedLoudness(double[][] weighted, int frames, int channels, int rate)
    {
        double[][] blocks = CollectBlocks(weighted, frames, channels, rate);

        // Absolute gate: drop anything below -70 LUFS, which is silence by any measure.
        double[] meanPower = GatedMeanPower(blocks, channels, AbsoluteGateLufs, out bool anyAbove);
        if (!anyAbove) return double.NegativeInfinity;

        // Relative gate: drop anything more than 10 LU below what's left, so a loud
        // impact isn't averaged down by its own decay tail.
        double relativeGate = LoudnessOf(meanPower, channels) + RelativeGateLu;

        double[] gatedPower = GatedMeanPower(blocks, channels, relativeGate, out bool anySurvived);

        // A very short clip can have every block fall below its own relative gate.
        // Fall back to the absolute-gated mean rather than reporting silence.
        return LoudnessOf(anySurvived ? gatedPower : meanPower, channels);
    }

    private static double[] GatedMeanPower(double[][] blocks, int channels, double gateLufs, out bool anyPassed)
    {
        double[] total = new double[channels];
        int passed = 0;

        foreach (double[] block in blocks)
        {
            if (LoudnessOf(block, channels) <= gateLufs) continue;

            for (int channel = 0; channel < channels; channel++)
            {
                total[channel] += block[channel];
            }

            passed++;
        }

        anyPassed = passed > 0;
        if (!anyPassed) return total;

        for (int channel = 0; channel < channels; channel++)
        {
            total[channel] /= passed;
        }

        return total;
    }
}
