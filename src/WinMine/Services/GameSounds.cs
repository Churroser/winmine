using System.Runtime.InteropServices;

namespace WinMine.Services;

/// <summary>Discrete sound effects the game can make.</summary>
public enum GameSound
{
    Click,
    Reveal,
    Flood,
    Flag,
    Unflag,
    Chord,
    Explode,
    Win,
}

/// <summary>
/// Tiny procedural audio bank. Every effect is synthesised into an in-memory PCM WAV once,
/// then handed to <c>winmm!PlaySound</c> with <c>SND_MEMORY | SND_ASYNC</c> — no asset files,
/// no NuGet audio stack, and playback starts immediately (important for rapid clicks).
/// </summary>
public static class GameSounds
{
    private const int SampleRate = 44100;
    private const int BytesPerSample = 2;

    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;

    private static readonly Dictionary<GameSound, IntPtr> Buffers = new();
    private static readonly List<GCHandle> Pins = new();
    private static readonly object Gate = new();

    private static bool _enabled = true;

    /// <summary>When false every <see cref="Play"/> call is a no-op.</summary>
    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>Plays a sound. Cheap enough to call from the board's move callback.</summary>
    /// <returns>True when the audio device accepted the block; false when muted or unavailable.</returns>
    public static bool Play(GameSound sound)
    {
        if (!_enabled)
        {
            return false;
        }

        try
        {
            return PlaySound(GetBuffer(sound), IntPtr.Zero, SndMemory | SndAsync | SndNoDefault);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Renders the WAV payload for a sound. Exposed for the self-test.</summary>
    public static byte[] RenderWav(GameSound sound) => sound switch
    {
        GameSound.Click => Build(0.022, t => Sine(t, 2100) * Envelope(t, 0.022, 260) * 0.4),
        GameSound.Reveal => Build(0.055, t => (Sine(t, 880) + 0.25 * Sine(t, 1760)) * Envelope(t, 0.055, 70) * 0.32),
        GameSound.Flood => Build(0.13, t => Sine(t, 620 + 480 * (t / 0.13)) * Envelope(t, 0.13, 26) * 0.26),
        GameSound.Flag => Build(0.05, t => (Sine(t, 1320) + 0.3 * Sine(t, 3960)) * Envelope(t, 0.05, 80) * 0.3),
        GameSound.Unflag => Build(0.05, t => Sine(t, 990) * Envelope(t, 0.05, 80) * 0.26),
        GameSound.Chord => Build(0.1, t => ChordTone(t)),
        GameSound.Explode => Build(0.55, t => BombNoise(t)),
        GameSound.Win => Build(0.62, t => WinArpeggio(t)),
        _ => Build(0.02, t => 0),
    };

    private static IntPtr GetBuffer(GameSound sound)
    {
        lock (Gate)
        {
            if (Buffers.TryGetValue(sound, out IntPtr pointer))
            {
                return pointer;
            }

            // Pin forever: PlaySound keeps reading the block while it plays asynchronously.
            GCHandle pin = GCHandle.Alloc(RenderWav(sound), GCHandleType.Pinned);
            Pins.Add(pin);
            pointer = pin.AddrOfPinnedObject();
            Buffers[sound] = pointer;
            return pointer;
        }
    }

    // ── synthesis ───────────────────────────────────────────────────────────

    private static byte[] Build(double seconds, Func<double, double> sample)
    {
        int frames = Math.Max(1, (int)(seconds * SampleRate));
        int dataBytes = frames * BytesPerSample;
        var wav = new byte[44 + dataBytes];

        WriteAscii(wav, 0, "RIFF");
        WriteInt(wav, 4, 36 + dataBytes);
        WriteAscii(wav, 8, "WAVE");
        WriteAscii(wav, 12, "fmt ");
        WriteInt(wav, 16, 16);                 // PCM chunk size
        WriteShort(wav, 20, 1);                // PCM
        WriteShort(wav, 22, 1);                // mono
        WriteInt(wav, 24, SampleRate);
        WriteInt(wav, 28, SampleRate * BytesPerSample);
        WriteShort(wav, 32, BytesPerSample);
        WriteShort(wav, 34, 16);
        WriteAscii(wav, 36, "data");
        WriteInt(wav, 40, dataBytes);

        for (int i = 0; i < frames; i++)
        {
            double t = (double)i / SampleRate;
            double value = Math.Clamp(sample(t), -1.0, 1.0);
            short pcm = (short)Math.Round(value * short.MaxValue);
            wav[44 + (i * 2)] = (byte)(pcm & 0xFF);
            wav[44 + (i * 2) + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return wav;
    }

    private static double Sine(double t, double frequency) => Math.Sin(2 * Math.PI * frequency * t);

    /// <summary>Fast attack, exponential decay; <paramref name="decay"/> is decay per second.</summary>
    private static double Envelope(double t, double length, double decay)
    {
        double attack = Math.Min(1.0, t / 0.004);
        return attack * Math.Exp(-decay * t) * (t >= length ? 0 : 1);
    }

    private static double ChordTone(double t)
    {
        double first = t < 0.045 ? Sine(t, 1500) * Envelope(t, 0.045, 90) * 0.3 : 0;
        double second = t >= 0.045
            ? Sine(t - 0.045, 1050) * Envelope(t - 0.045, 0.05, 90) * 0.3
            : 0;
        return first + second;
    }

    private static double BombNoise(double t)
    {
        // Deterministic pseudo noise so the self-test can hash a stable block.
        uint state = (uint)(t * 100000) * 1664525u + 1013904223u;
        state ^= state >> 13;
        double noise = ((state & 0xFFFF) / 32767.5) - 1.0;
        double rumble = Sine(t, 78 - 30 * Math.Min(1, t / 0.55)) * 0.75;
        double env = Math.Exp(-5.5 * t) * Math.Min(1.0, t / 0.0025);
        return (noise * 0.55 + rumble * 0.45) * env * 0.6;
    }

    private static double WinArpeggio(double t)
    {
        double[] notes = { 523.25, 659.25, 783.99, 1046.50 };
        const double noteLength = 0.15;
        int index = Math.Min(notes.Length - 1, (int)(t / noteLength));
        double local = t - (index * noteLength);
        double tone = Sine(local, notes[index]) + 0.22 * Sine(local, notes[index] * 2);
        double tail = Math.Exp(-6 * local) * Math.Min(1.0, local / 0.006);
        // Let the last note ring a little longer than the others.
        double gate = index == notes.Length - 1 ? 1.0 : local < noteLength ? 1.0 : 0.0;
        return tone * tail * gate * 0.34;
    }

    private static void WriteAscii(byte[] buffer, int offset, string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            buffer[offset + i] = (byte)text[i];
        }
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteShort(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    private static extern bool PlaySound(IntPtr data, IntPtr module, uint flags);
}
