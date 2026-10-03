using System;

namespace KCAccess.Core
{
    public enum Waveform
    {
        Sine,
        Square,
        Triangle,
        Noise
    }

    /// <summary>One segment of a cue: a tone that glides from StartHz to EndHz.</summary>
    public struct ToneSegment
    {
        public float StartHz;
        public float EndHz;
        public float Seconds;
        public float Volume;
        public Waveform Wave;

        public ToneSegment(float startHz, float endHz, float seconds, float volume = 0.5f, Waveform wave = Waveform.Sine)
        {
            StartHz = startHz;
            EndHz = endHz;
            Seconds = seconds;
            Volume = volume;
            Wave = wave;
        }
    }

    /// <summary>Procedurally generates short sound cues so the mod needs no audio asset files.</summary>
    public static class ToneSynth
    {
        public const int DefaultSampleRate = 44100;

        /// <summary>Renders the segments one after another into mono float samples in [-1, 1].</summary>
        public static float[] Render(int sampleRate, params ToneSegment[] segments)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            int total = 0;
            foreach (var s in segments) total += Math.Max(0, (int)(s.Seconds * sampleRate));
            var data = new float[Math.Max(total, 1)];
            int offset = 0;
            var rng = new Random(1234);
            foreach (var seg in segments)
            {
                int n = Math.Max(0, (int)(seg.Seconds * sampleRate));
                double phase = 0;
                // 5 ms attack / release envelope prevents clicks.
                int ramp = Math.Max(1, Math.Min(n / 4, (int)(0.005 * sampleRate)));
                for (int i = 0; i < n; i++)
                {
                    double t = n <= 1 ? 0 : (double)i / (n - 1);
                    double hz = seg.StartHz + (seg.EndHz - seg.StartHz) * t;
                    phase += 2.0 * Math.PI * hz / sampleRate;
                    double v;
                    switch (seg.Wave)
                    {
                        case Waveform.Square:
                            v = Math.Sin(phase) >= 0 ? 0.6 : -0.6;
                            break;
                        case Waveform.Triangle:
                            v = 2.0 / Math.PI * Math.Asin(Math.Sin(phase));
                            break;
                        case Waveform.Noise:
                            v = rng.NextDouble() * 2.0 - 1.0;
                            break;
                        default:
                            v = Math.Sin(phase);
                            break;
                    }
                    double env = 1.0;
                    if (i < ramp) env = (double)i / ramp;
                    else if (i > n - ramp) env = (double)(n - i) / ramp;
                    data[offset + i] = (float)(v * env * Clamp01(seg.Volume));
                }
                offset += n;
            }
            return data;
        }

        private static double Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        /// <summary>Maps a value in [0,1] to a pitch between low and high Hz (used for sliders / percentages).</summary>
        public static float PitchFor(float fraction, float lowHz = 300f, float highHz = 1200f)
        {
            if (float.IsNaN(fraction)) fraction = 0f;
            fraction = Math.Max(0f, Math.Min(1f, fraction));
            // Exponential mapping sounds linear to the ear.
            return (float)(lowHz * Math.Pow(highHz / lowHz, fraction));
        }
    }
}
