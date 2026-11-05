using WinMine.Core;

namespace WinMine.Services;

/// <summary>Persists <see cref="GameSettings"/> as JSON under %LOCALAPPDATA%\WinMine.</summary>
internal sealed class SettingsStore
{
    private readonly string _directory;
    private readonly string _file;

    public SettingsStore()
    {
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinMine");
        _file = Path.Combine(_directory, "settings.json");
    }

    public string FilePath => _file;

    public GameSettings Load()
    {
        try
        {
            return File.Exists(_file)
                ? GameSettings.FromJson(File.ReadAllText(_file))
                : new GameSettings();
        }
        catch (IOException)
        {
            return new GameSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new GameSettings();
        }
    }

    public void Save(GameSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_file, settings.ToJson());
        }
        catch (IOException)
        {
            // Settings are a convenience; a locked file must never break play.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
