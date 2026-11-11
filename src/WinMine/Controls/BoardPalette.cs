using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WinMine.Controls;

/// <summary>All brushes the board needs for one theme, resolved once per theme.</summary>
internal sealed class BoardColors
{
    public required Brush Surface { get; init; }

    public required Brush Hidden { get; init; }

    public required Brush HiddenHover { get; init; }

    public required Brush HiddenPressed { get; init; }

    public required Brush HiddenStroke { get; init; }

    /// <summary>Fill of a tile that is about to be opened by a chord.</summary>
    public required Brush ChordPreview { get; init; }

    public required Brush ChordPreviewStroke { get; init; }

    public required Brush Revealed { get; init; }

    public required Brush RevealedGrid { get; init; }

    public required Brush RevealedHover { get; init; }

    public required Brush Mine { get; init; }

    public required Brush Flag { get; init; }

    public required Brush WrongFlag { get; init; }

    public required Brush Exploded { get; init; }

    public required Brush ExplodedStroke { get; init; }

    public required Brush Focus { get; init; }

    public required Brush Transparent { get; init; }

    public required Brush[] Numbers { get; init; }
}

internal static class BoardPalette
{
    private static BoardColors? _light;
    private static BoardColors? _dark;

    public static BoardColors For(bool dark) => dark
        ? _dark ??= CreateDark()
        : _light ??= CreateLight();

    private static BoardColors CreateLight() => new()
    {
        Surface = Brush("#E9E9E9"),
        Hidden = Brush("#FFFFFF"),
        HiddenHover = Brush("#F2F8FF"),
        HiddenPressed = Brush("#E1EDFA"),
        HiddenStroke = Brush("#D6D6D6"),
        ChordPreview = Brush("#C8DDF5"),
        ChordPreviewStroke = Brush("#96B8DF"),
        Revealed = Brush("#E9E9E9"),
        RevealedGrid = Brush("#DADADA"),
        RevealedHover = Brush("#E0E0E0"),
        Mine = Brush("#1B1B1B"),
        Flag = Brush("#0F6CBD"),
        WrongFlag = Brush("#C42B1C"),
        Exploded = Brush("#F8C6CB"),
        ExplodedStroke = Brush("#C42B1C"),
        Focus = Brush("#0F6CBD"),
        Transparent = new SolidColorBrush(Colors.Transparent),
        Numbers = new[]
        {
            Brush("#1D6FD8"), Brush("#107C41"), Brush("#C42B1C"), Brush("#6B2FA0"),
            Brush("#8A5A00"), Brush("#0E7490"), Brush("#1B1B1B"), Brush("#737373"),
        },
    };

    private static BoardColors CreateDark() => new()
    {
        Surface = Brush("#1F1F1F"),
        Hidden = Brush("#333333"),
        HiddenHover = Brush("#3F3F3F"),
        HiddenPressed = Brush("#4E4E4E"),
        HiddenStroke = Brush("#4A4A4A"),
        ChordPreview = Brush("#2B4A6B"),
        ChordPreviewStroke = Brush("#4E7CA8"),
        Revealed = Brush("#1F1F1F"),
        RevealedGrid = Brush("#2E2E2E"),
        RevealedHover = Brush("#282828"),
        Mine = Brush("#EDEDED"),
        Flag = Brush("#60CDFF"),
        WrongFlag = Brush("#FF99A4"),
        Exploded = Brush("#5A1F22"),
        ExplodedStroke = Brush("#FF99A4"),
        Focus = Brush("#60CDFF"),
        Transparent = new SolidColorBrush(Colors.Transparent),
        Numbers = new[]
        {
            Brush("#6CB8FF"), Brush("#54D38A"), Brush("#FF8A80"), Brush("#C9A6FF"),
            Brush("#E5B567"), Brush("#5BD0DC"), Brush("#EDEDED"), Brush("#9E9E9E"),
        },
    };

    public static SolidColorBrush Brush(string hex) => new(ParseHex(hex));

    private static Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        return Color.FromArgb(
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16),
            Convert.ToByte(hex.Substring(6, 2), 16));
    }
}
