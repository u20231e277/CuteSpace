using System.Runtime.InteropServices;
using System.Text;

namespace CuteSpace.Services;

public sealed class CuteSoundService
{
    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(string pszSound, nint hmod, uint fdwSound);

    private const uint SndAsync = 0x0001;
    private const uint SndFilename = 0x00020000;

    /// <summary>
    /// Bump this version whenever synthesis parameters change so old cached
    /// WAV files are deleted and regenerated automatically.
    /// </summary>
    private const int SoundVersion = 2;

    private readonly string _soundFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CuteSpace",
        "sounds");

    public bool Enabled { get; set; } = true;
    public string Style { get; set; } = "cute";

    public CuteSoundService()
    {
        EnsureSounds();
    }

    public void Pop()
    {
        Play("pop");
    }

    public void Tap()
    {
        if (!Enabled)
        {
            return;
        }

        Play("tap");
    }

    public void Success()
    {
        Play("success");
    }

    public void Ding()
    {
        Play("ding");
    }

    private void Play(string sound)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            var fileName = $"{NormalizeStyle(Style)}-{sound}.wav";
            PlaySound(Path.Combine(_soundFolder, fileName), 0, SndFilename | SndAsync);
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(CuteSoundService), ex.ToString());
        }
    }

    private void EnsureSounds()
    {
        try
        {
            Directory.CreateDirectory(_soundFolder);
            PurgeStaleFiles();

            // ── cute: soft, round, pitch-slide portamento, gentle reverb tail ──
            GenerateCuteSound("cute-tap.wav",
                [(680, 720, 55, 0.14), (900, 940, 65, 0.11)]);
            GenerateCuteSound("cute-pop.wav",
                [(540, 580, 60, 0.13), (780, 820, 85, 0.13), (1060, 1100, 70, 0.09)]);
            GenerateCuteSound("cute-success.wav",
                [(640, 660, 85, 0.12), (840, 860, 85, 0.12), (1060, 1080, 110, 0.11)]);
            GenerateCuteSound("cute-ding.wav",
                [(880, 880, 100, 0.10), (1320, 1320, 140, 0.12), (1760, 1760, 200, 0.09)]);

            // ── forge: deeper, mechanical clicks, soft but present ──
            GenerateForgeSound("forge-tap.wav",
                [(280, 40, 0.16), (420, 35, 0.12)]);
            GenerateForgeSound("forge-pop.wav",
                [(250, 50, 0.16), (440, 60, 0.14), (620, 45, 0.10)]);
            GenerateForgeSound("forge-success.wav",
                [(340, 65, 0.14), (520, 65, 0.14), (700, 80, 0.11)]);
            GenerateForgeSound("forge-ding.wav",
                [(440, 90, 0.12), (660, 120, 0.13), (880, 160, 0.10)]);

            // ── flow: minimal zen water-drops ──
            GenerateFlowSound("flow-tap.wav",
                [(620, 40, 0.10)]);
            GenerateFlowSound("flow-pop.wav",
                [(540, 50, 0.09), (720, 65, 0.08)]);
            GenerateFlowSound("flow-success.wav",
                [(580, 65, 0.08), (740, 80, 0.07), (920, 90, 0.06)]);
            GenerateFlowSound("flow-ding.wav",
                [(880, 90, 0.08), (1320, 140, 0.08), (1760, 180, 0.06)]);
        }
        catch (Exception ex)
        {
            SafeLog.Write(nameof(CuteSoundService), ex.ToString());
        }
    }

    /// <summary>
    /// Delete all cached WAV files when the version stamp changes so they are
    /// regenerated with the latest synthesis parameters.
    /// </summary>
    private void PurgeStaleFiles()
    {
        var versionFile = Path.Combine(_soundFolder, ".version");
        var currentVersion = 0;

        if (File.Exists(versionFile) && int.TryParse(File.ReadAllText(versionFile).Trim(), out var v))
        {
            currentVersion = v;
        }

        if (currentVersion >= SoundVersion)
        {
            return;
        }

        foreach (var wav in Directory.EnumerateFiles(_soundFolder, "*.wav"))
        {
            try { File.Delete(wav); } catch { /* best-effort */ }
        }

        File.WriteAllText(versionFile, SoundVersion.ToString());
    }

    private static string NormalizeStyle(string style)
    {
        return style is "forge" or "flow" ? style : "cute";
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Common helpers
    // ═══════════════════════════════════════════════════════════════════════

    private const int SampleRate = 44100;
    private const short Channels = 1;
    private const short BitsPerSample = 16;

    /// <summary>
    /// Blend of sine and triangle wave for a warmer, rounder timbre.
    /// <paramref name="mix"/> 0 = pure sine, 1 = pure triangle.
    /// </summary>
    private static double WarmWave(double phase, double mix = 0.25)
    {
        var sine = Math.Sin(phase);
        // Triangle wave from phase (normalised to 0..2π range)
        var p = (phase % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
        double triangle;
        if (p < Math.PI)
            triangle = 2.0 * p / Math.PI - 1.0;
        else
            triangle = 3.0 - 2.0 * p / Math.PI;

        return sine * (1.0 - mix) + triangle * mix;
    }

    /// <summary>
    /// Soft attack / release envelope.
    /// Attack is an exponential rise; sustain holds; release is exponential decay.
    /// </summary>
    private static double SoftEnvelope(int i, int total, double attackFraction = 0.15, double releaseFraction = 0.40)
    {
        if (total <= 0) return 0;
        var t = (double)i / total;

        if (t < attackFraction)
        {
            // Exponential ease-in
            var a = t / attackFraction;
            return 1.0 - Math.Exp(-4.0 * a);
        }

        if (t > 1.0 - releaseFraction)
        {
            // Exponential ease-out
            var r = (t - (1.0 - releaseFraction)) / releaseFraction;
            return Math.Exp(-4.0 * r);
        }

        return 1.0;
    }

    /// <summary>
    /// Water-drop envelope: instant attack, long exponential decay.
    /// </summary>
    private static double DropEnvelope(int i, int total)
    {
        if (total <= 0) return 0;
        var t = (double)i / total;
        // Tiny 2% attack ramp to avoid click
        if (t < 0.02)
            return t / 0.02;
        return Math.Exp(-6.0 * t);
    }

    /// <summary>
    /// Append a reverb-like tail of decaying echoes to the sample buffer.
    /// </summary>
    private static void AppendReverbTail(List<short> samples, int tailMs, double decay, int echoCount = 3)
    {
        var tailSamples = SampleRate * tailMs / 1000;
        var sourceLen = samples.Count;

        // Extend buffer for the tail
        samples.AddRange(Enumerable.Repeat((short)0, tailSamples));

        var delayStep = tailSamples / Math.Max(1, echoCount);
        for (var echo = 1; echo <= echoCount; echo++)
        {
            var offset = echo * delayStep;
            var gain = Math.Pow(decay, echo);
            // Mix echoes of the last portion of the source into the tail
            var echoSource = Math.Min(sourceLen, SampleRate * 40 / 1000); // echo last ~40ms
            for (var j = 0; j < echoSource && sourceLen - echoSource + j >= 0; j++)
            {
                var idx = sourceLen + offset - echoSource + j;
                if (idx >= 0 && idx < samples.Count)
                {
                    var existing = samples[(int)idx];
                    var echoVal = samples[sourceLen - echoSource + j] * gain;
                    var mixed = Math.Clamp(existing + echoVal, short.MinValue, short.MaxValue);
                    samples[(int)idx] = (short)mixed;
                }
            }
        }
    }

    /// <summary>Add a silent gap between notes.</summary>
    private static void AppendSilence(List<short> samples, int ms)
    {
        samples.AddRange(Enumerable.Repeat((short)0, SampleRate * ms / 1000));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Style-specific generators
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cute style: warm wave, portamento pitch slide, soft envelope, reverb tail.
    /// Notes are (startFreq, endFreq, durationMs, volume).
    /// </summary>
    private void GenerateCuteSound(string fileName,
        IReadOnlyList<(int FreqStart, int FreqEnd, int Ms, double Volume)> notes)
    {
        var path = Path.Combine(_soundFolder, fileName);
        if (File.Exists(path)) return;

        var samples = new List<short>();

        foreach (var (freqStart, freqEnd, ms, volume) in notes)
        {
            var count = SampleRate * ms / 1000;
            double phase = 0;
            for (var i = 0; i < count; i++)
            {
                var t = (double)i / Math.Max(1, count);
                // Portamento: smooth frequency slide
                var freq = freqStart + (freqEnd - freqStart) * t;
                var envelope = SoftEnvelope(i, count, 0.12, 0.45);
                var value = WarmWave(phase, 0.20) * volume * envelope;
                samples.Add((short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue));
                phase += 2 * Math.PI * freq / SampleRate;
            }

            AppendSilence(samples, 12);
        }

        // Gentle reverb tail
        AppendReverbTail(samples, 80, 0.35, 3);

        WriteWav(path, samples);
    }

    /// <summary>
    /// Forge style: deeper tones, sharp but short click-like envelope, slight
    /// low-frequency sub-harmonic for body. No reverb.
    /// Notes are (frequency, durationMs, volume).
    /// </summary>
    private void GenerateForgeSound(string fileName,
        IReadOnlyList<(int Frequency, int Ms, double Volume)> notes)
    {
        var path = Path.Combine(_soundFolder, fileName);
        if (File.Exists(path)) return;

        var samples = new List<short>();

        foreach (var (frequency, ms, volume) in notes)
        {
            var count = SampleRate * ms / 1000;
            for (var i = 0; i < count; i++)
            {
                // Snappy envelope: very short attack, quick release
                var envelope = SoftEnvelope(i, count, 0.05, 0.55);
                // Main tone (warm wave with more triangle character)
                var main = WarmWave(2 * Math.PI * frequency * i / SampleRate, 0.40);
                // Sub-harmonic one octave below at 30% level for body
                var sub = Math.Sin(2 * Math.PI * (frequency / 2.0) * i / SampleRate) * 0.30;
                var value = (main + sub) * volume * envelope;
                samples.Add((short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue));
            }

            AppendSilence(samples, 10);
        }

        WriteWav(path, samples);
    }

    /// <summary>
    /// Flow style: water-drop character — instant attack, long exponential
    /// decay, pure-ish sine with a hint of second harmonic.
    /// Notes are (frequency, durationMs, volume).
    /// </summary>
    private void GenerateFlowSound(string fileName,
        IReadOnlyList<(int Frequency, int Ms, double Volume)> notes)
    {
        var path = Path.Combine(_soundFolder, fileName);
        if (File.Exists(path)) return;

        var samples = new List<short>();

        foreach (var (frequency, ms, volume) in notes)
        {
            var count = SampleRate * ms / 1000;
            for (var i = 0; i < count; i++)
            {
                var envelope = DropEnvelope(i, count);
                // Mostly sine with subtle 2nd harmonic for shimmer
                var main = Math.Sin(2 * Math.PI * frequency * i / SampleRate);
                var harmonic = Math.Sin(2 * Math.PI * frequency * 2 * i / SampleRate) * 0.12;
                var value = (main + harmonic) * volume * envelope;
                samples.Add((short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue));
            }

            AppendSilence(samples, 25); // slightly longer gap — zen pacing
        }

        WriteWav(path, samples);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  WAV writer
    // ═══════════════════════════════════════════════════════════════════════

    private static void WriteWav(string path, List<short> samples)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        var dataLength = samples.Count * sizeof(short);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(Channels);
        writer.Write(SampleRate);
        writer.Write(SampleRate * Channels * BitsPerSample / 8);
        writer.Write((short)(Channels * BitsPerSample / 8));
        writer.Write(BitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);
        foreach (var sample in samples)
        {
            writer.Write(sample);
        }
    }
}
