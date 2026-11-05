namespace WinMine.Core;

/// <summary>Best completion times in whole seconds, keyed by <see cref="DifficultyPresets.Key"/>.</summary>
public sealed class BestTimes
{
    private readonly Dictionary<string, int> _times = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> All => _times;

    public int? Get(string key) => _times.TryGetValue(key, out var seconds) ? seconds : null;

    public int? Get(DifficultyLevel level) => Get(DifficultyPresets.Key(level));

    /// <summary>Records a win. Returns true when it beat the previous best (or set the first one).</summary>
    public bool TryRecord(string key, int seconds)
    {
        if (seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        if (_times.TryGetValue(key, out int existing) && existing <= seconds)
        {
            return false;
        }

        _times[key] = seconds;
        return true;
    }

    public void Set(string key, int seconds)
    {
        if (seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        _times[key] = seconds;
    }

    public void Clear() => _times.Clear();

    public void Load(IEnumerable<KeyValuePair<string, int>> values)
    {
        _times.Clear();
        foreach (var pair in values)
        {
            if (pair.Value >= 0)
            {
                _times[pair.Key] = pair.Value;
            }
        }
    }
}
