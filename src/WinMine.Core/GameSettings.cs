using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinMine.Core;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Everything the app persists between runs. Serialized as JSON under %LOCALAPPDATA%\WinMine.</summary>
public sealed class GameSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    public DifficultyLevel LastDifficulty { get; set; } = DifficultyLevel.Beginner;

    public BoardPreset CustomBoard { get; set; } = new(16, 16, 40);

    public Dictionary<string, int> BestTimes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ShowBestTimeBadge { get; set; } = true;

    public bool SoundEnabled { get; set; } = true;

    [JsonIgnore]
    public string LastDifficultyKey => LastDifficulty switch
    {
        DifficultyLevel.Custom => "Custom",
        _ => DifficultyPresets.Key(LastDifficulty),
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static GameSettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GameSettings();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<GameSettings>(json, SerializerOptions);
            if (parsed is null)
            {
                return new GameSettings();
            }

            parsed.BestTimes ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            parsed.CustomBoard = SanitizeCustom(parsed.CustomBoard);
            return parsed;
        }
        catch (JsonException)
        {
            return new GameSettings();
        }
    }

    private static BoardPreset SanitizeCustom(BoardPreset? preset)
    {
        if (preset is null)
        {
            return new BoardPreset(16, 16, 40);
        }

        int rows = Math.Clamp(preset.Rows, Minefield.MinRows, Minefield.MaxRows);
        int columns = Math.Clamp(preset.Columns, Minefield.MinColumns, Minefield.MaxColumns);
        int mines = Math.Clamp(preset.Mines, 1, rows * columns - 1);
        return new BoardPreset(rows, columns, mines);
    }
}
