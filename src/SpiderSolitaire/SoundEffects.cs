using System.Media;

namespace SpiderSolitaire;

internal enum Sound
{
    Place,
    Deal,
    CompleteSuit,
    Invalid,
    Win,
}

/// <summary>
/// Short sound effects, synthesised as WAV data when first needed so the game ships no
/// audio files. Playing never throws: a machine with no audio device just stays quiet.
/// </summary>
internal sealed class SoundEffects : IDisposable
{
    private const int SampleRate = 22050;

    private readonly Dictionary<Sound, SoundPlayer> _players = new();

    public bool Enabled { get; set; } = true;

    public void Play(Sound sound)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            if (!_players.TryGetValue(sound, out SoundPlayer? player))
            {
                player = new SoundPlayer(new MemoryStream(ToWave(Synthesize(sound))));
                player.Load();
                _players[sound] = player;
            }

            player.Play();
        }
        catch (Exception)
        {
            // No audio device, or the sound system is busy; the game carries on silently.
        }
    }

    public void Dispose()
    {
        foreach (SoundPlayer player in _players.Values)
        {
            player.Stream?.Dispose();
            player.Dispose();
        }

        _players.Clear();
    }

    private static float[] Synthesize(Sound sound)
    {
        Random random = new(1);
        return sound switch
        {
            Sound.Place => CardSnap(random, 0.05, 0.5f),
            Sound.Deal => Riffle(random),
            Sound.CompleteSuit => Chime(new[] { 659.3, 830.6, 987.8 }, 0.07, 0.45),
            Sound.Invalid => Bonk(),
            _ => Chime(new[] { 523.3, 659.3, 784.0, 1046.5, 1318.5 }, 0.11, 0.9),
        };
    }

    /// <summary>A card landing on the table: a burst of filtered noise over a soft thump.</summary>
    private static float[] CardSnap(Random random, double seconds, float volume)
    {
        float[] samples = new float[(int)(SampleRate * seconds)];
        float filtered = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / SampleRate;
            float noise = (float)(random.NextDouble() * 2 - 1);
            filtered += (noise - filtered) * 0.35f;
            double envelope = Math.Exp(-t / 0.009);
            double thump = Math.Sin(2 * Math.PI * 150 * t) * Math.Exp(-t / 0.02) * 0.5;
            samples[i] = (float)((filtered * 1.4 + thump) * envelope) * volume;
        }

        return samples;
    }

    /// <summary>Ten quick snaps, one per card dealt, timed to match the deal animation.</summary>
    private static float[] Riffle(Random random)
    {
        float[] samples = new float[(int)(SampleRate * 0.55)];
        for (int card = 0; card < 10; card++)
        {
            float[] snap = CardSnap(random, 0.04, 0.32f);
            int start = (int)(SampleRate * (card * 0.045 + random.NextDouble() * 0.008));
            for (int i = 0; i < snap.Length && start + i < samples.Length; i++)
            {
                samples[start + i] += snap[i];
            }
        }

        return samples;
    }

    /// <summary>Bell-like notes struck one after another and left to ring.</summary>
    private static float[] Chime(double[] notes, double spacing, double ring)
    {
        float[] samples = new float[(int)(SampleRate * (spacing * notes.Length + ring))];
        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(SampleRate * spacing * n);
            double decay = n == notes.Length - 1 ? ring / 2.5 : ring / 4;
            for (int i = start; i < samples.Length; i++)
            {
                double t = (double)(i - start) / SampleRate;
                double envelope = Math.Min(1, t / 0.004) * Math.Exp(-t / decay);
                double tone = Math.Sin(2 * Math.PI * notes[n] * t) + 0.3 * Math.Sin(2 * Math.PI * notes[n] * 2 * t) + 0.1 * Math.Sin(2 * Math.PI * notes[n] * 3 * t);
                samples[i] += (float)(tone * envelope * 0.16);
            }
        }

        return samples;
    }

    /// <summary>A low, short knock for a move that cannot be made.</summary>
    private static float[] Bonk()
    {
        float[] samples = new float[(int)(SampleRate * 0.14)];
        double phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / SampleRate;
            double frequency = 210 - 60 * t / 0.14;
            phase += 2 * Math.PI * frequency / SampleRate;
            double envelope = Math.Min(1, t / 0.003) * Math.Exp(-t / 0.04);
            samples[i] = (float)(Math.Sin(phase) * envelope * 0.4);
        }

        return samples;
    }

    /// <summary>16-bit mono PCM in a RIFF/WAVE container, which is what <see cref="SoundPlayer"/> plays.</summary>
    private static byte[] ToWave(float[] samples)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        int dataBytes = samples.Length * 2;

        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);

        foreach (float sample in samples)
        {
            writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }

        writer.Flush();
        return stream.ToArray();
    }
}
