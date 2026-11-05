namespace WinMine.Core;

using System.Text.Json.Serialization;

public enum DifficultyLevel
{
    Beginner,
    Intermediate,
    Expert,
    Custom,
}

/// <summary>A rectangular board definition: <paramref name="Columns"/> across, <paramref name="Rows"/> down.</summary>
public sealed record BoardPreset(int Rows, int Columns, int Mines)
{
    [JsonIgnore]
    public int CellCount => Rows * Columns;

    public static BoardPreset Beginner { get; } = new(9, 9, 10);

    public static BoardPreset Intermediate { get; } = new(16, 16, 40);

    public static BoardPreset Expert { get; } = new(16, 30, 99);
}

public static class DifficultyPresets
{
    public static BoardPreset? Get(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Beginner => BoardPreset.Beginner,
        DifficultyLevel.Intermediate => BoardPreset.Intermediate,
        DifficultyLevel.Expert => BoardPreset.Expert,
        _ => null,
    };

    /// <summary>Stable key used for persisted best times.</summary>
    public static string Key(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Beginner => "Beginner",
        DifficultyLevel.Intermediate => "Intermediate",
        DifficultyLevel.Expert => "Expert",
        _ => "Custom",
    };

    public static string DisplayName(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Beginner => "初级",
        DifficultyLevel.Intermediate => "中级",
        DifficultyLevel.Expert => "高级",
        _ => "自定义",
    };

    public static bool TryParse(string? name, out DifficultyLevel level)
    {
        level = DifficultyLevel.Beginner;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var candidate in Enum.GetValues<DifficultyLevel>())
        {
            if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                level = candidate;
                return true;
            }
        }

        return false;
    }
}
