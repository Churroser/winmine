using WinMine.Core;
using Xunit;

namespace WinMine.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void Settings_survive_a_json_round_trip()
    {
        var settings = new GameSettings
        {
            Theme = AppTheme.Dark,
            LastDifficulty = DifficultyLevel.Expert,
            CustomBoard = new BoardPreset(12, 18, 30),
            BestTimes = { ["Beginner"] = 12, ["Expert"] = 188 },
            ShowBestTimeBadge = false,
        };

        var restored = GameSettings.FromJson(settings.ToJson());

        Assert.Equal(AppTheme.Dark, restored.Theme);
        Assert.Equal(DifficultyLevel.Expert, restored.LastDifficulty);
        Assert.Equal(new BoardPreset(12, 18, 30), restored.CustomBoard);
        Assert.Equal(12, restored.BestTimes["Beginner"]);
        Assert.Equal(188, restored.BestTimes["Expert"]);
        Assert.False(restored.ShowBestTimeBadge);
    }

    [Fact]
    public void Broken_json_falls_back_to_defaults()
    {
        var restored = GameSettings.FromJson("{ this is not json ");
        Assert.Equal(AppTheme.System, restored.Theme);
        Assert.Equal(DifficultyLevel.Beginner, restored.LastDifficulty);
        Assert.Equal(new BoardPreset(16, 16, 40), restored.CustomBoard);
    }

    [Fact]
    public void Out_of_range_custom_boards_are_clamped()
    {
        var settings = GameSettings.FromJson("""
            { "CustomBoard": { "Rows": 900, "Columns": 1, "Mines": 99999 } }
            """);

        Assert.Equal(Minefield.MaxRows, settings.CustomBoard.Rows);
        Assert.Equal(Minefield.MinColumns, settings.CustomBoard.Columns);
        Assert.Equal(settings.CustomBoard.Rows * settings.CustomBoard.Columns - 1, settings.CustomBoard.Mines);
    }

    [Fact]
    public void Best_times_only_improve()
    {
        var best = new BestTimes();

        Assert.True(best.TryRecord("Beginner", 30));
        Assert.False(best.TryRecord("Beginner", 45));
        Assert.False(best.TryRecord("Beginner", 30));
        Assert.True(best.TryRecord("Beginner", 21));
        Assert.Equal(21, best.Get("Beginner"));
        Assert.Null(best.Get("Expert"));

        best.Clear();
        Assert.Empty(best.All);
    }

    [Fact]
    public void Presets_match_the_classic_windows_definitions()
    {
        Assert.Equal((9, 9, 10), (BoardPreset.Beginner.Rows, BoardPreset.Beginner.Columns, BoardPreset.Beginner.Mines));
        Assert.Equal((16, 16, 40), (BoardPreset.Intermediate.Rows, BoardPreset.Intermediate.Columns, BoardPreset.Intermediate.Mines));
        Assert.Equal((16, 30, 99), (BoardPreset.Expert.Rows, BoardPreset.Expert.Columns, BoardPreset.Expert.Mines));
        Assert.True(Minefield.IsValid(9, 9, 10, out _));
        Assert.False(Minefield.IsValid(9, 9, 82, out string? error));
        Assert.NotNull(error);
    }
}
