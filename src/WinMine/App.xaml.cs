using Microsoft.UI.Xaml;

namespace WinMine;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => LogCrash(e.Exception);
    }

    /// <summary>When set (--capture DIR), the app renders scripted states to PNG and exits.</summary>
    public static string? CaptureDirectory { get; private set; }

    /// <summary>When set (--selftest FILE), the app runs the scripted interaction suite and exits.</summary>
    public static string? SelfTestPath { get; private set; }

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinMine");

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var argv = Environment.GetCommandLineArgs();
        for (int i = 0; i < argv.Length - 1; i++)
        {
            if (string.Equals(argv[i], "--capture", StringComparison.OrdinalIgnoreCase))
            {
                CaptureDirectory = argv[i + 1];
            }
            else if (string.Equals(argv[i], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                SelfTestPath = argv[i + 1];
            }
        }

        _window = new MainWindow();
        _window.Activate();
    }

    public static void LogCrash(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(
                Path.Combine(LogDirectory, "crash.log"),
                $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}");
        }
        catch
        {
            // logging must never take the app down
        }
    }
}
