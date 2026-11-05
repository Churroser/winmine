using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinMine.Services;

/// <summary>
/// Wrapper around the WinUI built-in control sound player
/// (<see cref="Microsoft.UI.Xaml.Controls.ElementSoundPlayer"/> + <see cref="ElementSoundKind"/>),
/// i.e. the Fluent control sound bank that ships with the platform. It only carries UI-semantic
/// sounds (invoke / focus / show / hide / navigate), so gameplay effects go through
/// <see cref="GameSounds"/> instead.
/// </summary>
public static class UiSounds
{
    private static bool _initialised;
    private static bool _available = true;
    private static bool _played;

    public static bool Enabled { get; set; } = true;

    /// <summary>True until a platform call throws (e.g. no audio endpoint at all).</summary>
    public static bool Available => _available;

    /// <summary>True once the platform player accepted a call.</summary>
    public static bool HasPlayed => _played;

    public static void Play(ElementSoundKind kind, double volume = 0.7)
    {
        if (!Enabled || !_available)
        {
            return;
        }

        try
        {
            if (!_initialised)
            {
                // On = always play, regardless of the system "sound effects" checkbox.
                ElementSoundPlayer.State = ElementSoundPlayerState.On;
                _initialised = true;
            }

            ElementSoundPlayer.Volume = Math.Clamp(volume, 0, 1);
            ElementSoundPlayer.Play(kind);
            _played = true;
        }
        catch (Exception)
        {
            // Sound is decoration: never let a missing audio endpoint break the game.
            _available = false;
        }
    }
}
