using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinMine.Controls;
using WinMine.Core;
using WinMine.Services;
using WinMine.Views;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;

namespace WinMine;

public sealed partial class MainWindow : Window
{
    private const double TitleBarHeight = 48;
    private const double MinContentWidth = 420;
    private const double NavRailWidth = 48;

    private readonly SettingsStore _store = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _stopwatch = new();
    private readonly BestTimes _bestTimes = new();

    private GameSettings _settings = new();
    private DifficultyLevel _difficulty = DifficultyLevel.Beginner;
    private BoardPreset _preset = BoardPreset.Beginner;
    private Minefield? _board;
    private Brush? _neutralStatusBrush;
    private bool _suppressDifficultyChanges;
    private bool _suppressNavChanges;
    private bool _modalOpen;
    private bool _applyingLanguage;
    private bool _loaded;

    public MainWindow()
    {
        InitializeComponent();
        Title = Loc.T("app.title");
        ConfigureChrome();

        RootGrid.Loaded += RootGrid_Loaded;
        RootGrid.ActualThemeChanged += (_, _) => ApplyCaptionButtonColors();

        // Window-level shortcuts are handled here instead of with KeyboardAccelerator objects:
        // an accelerator attached to the root grid makes WinUI pop an "F2" keytip whenever the
        // pointer rests on the board, which is exactly the tooltip we do not want.
        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), handledEventsToo: true);

        _timer.Tick += (_, _) => UpdateTimerText(ElapsedSeconds());
        Board.MoveCompleted += Board_MoveCompleted;
        Board.PressStateChanged += Board_PressStateChanged;

        SettingsView.ThemeChanged += SettingsView_ThemeChanged;
        SettingsView.SoundChanged += SettingsView_SoundChanged;
        SettingsView.LanguageChanged += SettingsView_LanguageChanged;
        SettingsView.ResetBestTimesRequested += SettingsView_ResetBestTimesRequested;
        SettingsView.ViewSourceRequested += SettingsView_ViewSourceRequested;

        Closed += (_, _) => SaveSettings();
    }

    private enum FaceKind
    {
        Smile,
        Surprised,
        Cool,
        Dead,
    }

    private enum StatusTone
    {
        Neutral,
        Success,
        Error,
    }

    private void ConfigureChrome()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;

        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
        else if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }

        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "winmine.ico");
        if (File.Exists(icon))
        {
            AppWindow.SetIcon(icon);
        }
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= RootGrid_Loaded;
        _neutralStatusBrush = StatusText.Foreground;
        _captureDirectory = App.CaptureDirectory;
        _selfTestMode = App.SelfTestPath is not null;

        _settings = _store.Load();
        _bestTimes.Load(_settings.BestTimes);
        ApplyThemeSetting();
        ApplySoundSetting();
        ApplyLanguage();

        _loaded = true;
        var level = _settings.LastDifficulty;
        var preset = level == DifficultyLevel.Custom
            ? _settings.CustomBoard
            : DifficultyPresets.Get(level) ?? BoardPreset.Beginner;
        StartNewGame(preset, level);

        if (_captureDirectory is not null)
        {
            await RunCaptureAsync();
        }
        else if (App.SelfTestPath is { } selfTestPath)
        {
            await RunSelfTestAsync(selfTestPath);
        }

        await Task.CompletedTask;
    }

    // ── 局内流程 ────────────────────────────────────────────────────────────────

    private void StartNewGame(BoardPreset preset, DifficultyLevel level, int? seed = null)
    {
        _preset = preset;
        _difficulty = level;
        _settings.LastDifficulty = level;
        if (level == DifficultyLevel.Custom)
        {
            _settings.CustomBoard = preset;
        }

        _board = new Minefield(preset.Rows, preset.Columns, preset.Mines, seed);
        Board.LoadBoard(_board);

        _stopwatch.Reset();
        _timer.Stop();
        UpdateTimerText(0);
        SetFace(FaceKind.Smile);
        SetStatus(string.Empty, StatusTone.Neutral);
        UpdateCounters();
        UpdateBestTimeText();
        SelectDifficulty(level);

        FitWindowToBoard();
        SaveSettings();
    }

    private void Board_MoveCompleted(object? sender, MoveResult result)
    {
        if (_board is null)
        {
            return;
        }

        if (!_stopwatch.IsRunning && result.Status is GameStatus.Playing or GameStatus.Won or GameStatus.Lost)
        {
            _stopwatch.Start();
            _timer.Start();
        }

        UpdateCounters();

        PlayMoveSound(result);

        switch (result.Status)
        {
            case GameStatus.Won:
                HandleWin();
                break;
            case GameStatus.Lost:
                _stopwatch.Stop();
                _timer.Stop();
                UpdateTimerText(ElapsedSeconds());
                SetFace(FaceKind.Dead);
                SetStatus(Loc.F("status.exploded", ElapsedSeconds()), StatusTone.Error);
                break;
            default:
                SetFace(FaceKind.Smile);
                break;
        }
    }

    /// <summary>Maps a board move to an effect. Explosion / victory win over the click sounds.</summary>
    private static void PlayMoveSound(MoveResult result)
    {
        if (result.Status == GameStatus.Lost && result.Outcome == MoveOutcome.Lost)
        {
            GameSounds.Play(GameSound.Explode);
            return;
        }

        if (result.Status == GameStatus.Won)
        {
            GameSounds.Play(GameSound.Win);
            return;
        }

        switch (result.Outcome)
        {
            case MoveOutcome.Flagged:
                GameSounds.Play(GameSound.Flag);
                break;
            case MoveOutcome.Unflagged:
                GameSounds.Play(GameSound.Unflag);
                break;
            case MoveOutcome.Chorded:
                GameSounds.Play(GameSound.Chord);
                break;
            case MoveOutcome.Revealed:
                GameSounds.Play(result.Revealed.Count > 3 ? GameSound.Flood : GameSound.Reveal);
                break;
        }
    }

    private void Board_PressStateChanged(object? sender, bool pressed)
    {
        if (_board is null)
        {
            return;
        }

        if (pressed)
        {
            if (_board.Status is GameStatus.Ready or GameStatus.Playing)
            {
                SetFace(FaceKind.Surprised);
            }

            return;
        }

        SetFace(_board.Status switch
        {
            GameStatus.Won => FaceKind.Cool,
            GameStatus.Lost => FaceKind.Dead,
            _ => FaceKind.Smile,
        });
    }

    private void HandleWin()
    {
        _stopwatch.Stop();
        _timer.Stop();
        int seconds = ElapsedSeconds();
        UpdateTimerText(seconds);
        SetFace(FaceKind.Cool);

        bool record = false;
        if (_difficulty != DifficultyLevel.Custom)
        {
            record = _bestTimes.TryRecord(DifficultyPresets.Key(_difficulty), seconds);
            if (record)
            {
                PersistBestTimes();
                UpdateBestTimeText();
            }
        }

        SetStatus(
            record ? Loc.F("status.newRecord", seconds) : Loc.F("status.won", seconds),
            StatusTone.Success);
    }

    private int ElapsedSeconds() => (int)Math.Min(999, Math.Floor(_stopwatch.Elapsed.TotalSeconds));

    private void UpdateTimerText(int seconds) => TimerText.Text = Math.Clamp(seconds, 0, 999).ToString("000");

    private void UpdateCounters()
    {
        if (_board is null)
        {
            MineCounterText.Text = "000";
            return;
        }

        int remaining = _board.RemainingMines;
        MineCounterText.Text = remaining < 0
            ? "-" + Math.Min(99, -remaining).ToString("00")
            : Math.Min(999, remaining).ToString("000");
    }

    private void UpdateBestTimeText()
    {
        if (_difficulty == DifficultyLevel.Custom)
        {
            BestTimeText.Text = Loc.T("footer.customNoBest");
            return;
        }

        var best = _bestTimes.Get(_difficulty);
        BestTimeText.Text = best is null
            ? Loc.F("footer.noBest", Loc.DifficultyName(_difficulty))
            : Loc.F("footer.best", Loc.DifficultyName(_difficulty), best);
    }

    private void SetFace(FaceKind face)
    {
        FaceText.Text = face switch
        {
            FaceKind.Surprised => "😮",
            FaceKind.Cool => "😎",
            FaceKind.Dead => "😵",
            _ => "🙂",
        };
    }

    private Brush SecondaryBrush() => new SolidColorBrush(
        RootGrid.ActualTheme == ElementTheme.Dark
            ? Color.FromArgb(255, 0xC5, 0xC5, 0xC5)
            : Color.FromArgb(255, 0x5D, 0x5D, 0x5D));

    private void SetStatus(string text, StatusTone tone)
    {
        StatusText.Text = text;
        bool dark = RootGrid.ActualTheme == ElementTheme.Dark;
        StatusText.Foreground = tone switch
        {
            StatusTone.Success => new SolidColorBrush(dark ? Color.FromArgb(255, 0x6C, 0xCB, 0x5F) : Color.FromArgb(255, 0x0F, 0x7B, 0x0F)),
            StatusTone.Error => new SolidColorBrush(dark ? Color.FromArgb(255, 0xFF, 0x99, 0xA4) : Color.FromArgb(255, 0xC4, 0x2B, 0x1C)),
            _ => _neutralStatusBrush ?? StatusText.Foreground,
        };
    }

    private void SelectDifficulty(DifficultyLevel level, bool raiseEvent = false)
    {
        _suppressDifficultyChanges = !raiseEvent;
        DifficultyBar.SelectedItem = level switch
        {
            DifficultyLevel.Intermediate => ItemIntermediate,
            DifficultyLevel.Expert => ItemExpert,
            DifficultyLevel.Custom => ItemCustom,
            _ => ItemBeginner,
        };
        _suppressDifficultyChanges = false;
    }

    private void SaveSettings()
    {
        if (_captureDirectory is not null || _selfTestMode)
        {
            return; // 截图/自检脚本运行不能污染玩家配置
        }

        _settings.BestTimes = new Dictionary<string, int>(_bestTimes.All, StringComparer.OrdinalIgnoreCase);
        _store.Save(_settings);
    }

    private void PersistBestTimes() => SaveSettings();

    // ── 窗口尺寸 ────────────────────────────────────────────────────────────────

    private void FitWindowToBoard()
    {
        if (_board is null || RootGrid.XamlRoot is null)
        {
            return;
        }

        double scale = RootGrid.XamlRoot.RasterizationScale;
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        double maxWidth = (work.Width / scale) - 32;
        double maxHeight = (work.Height / scale) - 32;

        double chosen = 18;
        for (double size = 46; size >= 18; size -= 1)
        {
            // The board must be resized *before* measuring, otherwise the panel reports the
            // previous tile size and the search collapses to the smallest board.
            Board.UpdateCellSize(size);
            double width = Math.Max(Board.MeasureWidthFor(size) + 40 + NavRailWidth, MinContentWidth);
            Nav.Measure(new Size(width, double.PositiveInfinity));
            double height = TitleBarHeight + Nav.DesiredSize.Height;
            if (width <= maxWidth && height <= maxHeight)
            {
                chosen = size;
                break;
            }
        }

        Board.UpdateCellSize(chosen);

        double contentWidth = Math.Max(Board.MeasureWidthFor(chosen) + 40 + NavRailWidth, MinContentWidth);
        Nav.Measure(new Size(contentWidth, double.PositiveInfinity));
        double contentHeight = TitleBarHeight + Nav.DesiredSize.Height + 2;

        int pixelWidth = (int)Math.Ceiling(contentWidth * scale);
        int pixelHeight = (int)Math.Ceiling(contentHeight * scale);

        AppWindow.ResizeClient(new SizeInt32(pixelWidth, pixelHeight));
        AppWindow.Move(new PointInt32(
            work.X + Math.Max(0, (work.Width - pixelWidth) / 2),
            work.Y + Math.Max(0, (work.Height - pixelHeight) / 2)));
    }

    // ── 主题与标题栏配色 ────────────────────────────────────────────────────────

    // ── 语言 ────────────────────────────────────────────────────────────────────

    /// <summary>Resolves the persisted preference (System → OS display language) and renders it.</summary>
    private void ApplyLanguage()
    {
        Loc.SetLanguage(LanguageResolver.Resolve(_settings.Language));
        ApplyLocalization();
    }

    /// <summary>Pushes every localized string into the live visual tree.</summary>
    private void ApplyLocalization()
    {
        Title = Loc.T("app.title");
        TitleText.Text = Loc.T("app.title");

        ItemBeginner.Text = Loc.T("difficulty.beginner");
        ItemIntermediate.Text = Loc.T("difficulty.intermediate");
        ItemExpert.Text = Loc.T("difficulty.expert");
        ItemCustom.Text = Loc.T("difficulty.custom");

        ToolTipService.SetToolTip(NewGameButton, Loc.T("button.newGame"));
        AutomationProperties.SetName(NewGameButton, Loc.T("button.newGame"));
        ToolTipService.SetToolTip(ResetButton, Loc.T("button.restart"));
        AutomationProperties.SetName(ResetButton, Loc.T("button.restart"));
        AutomationProperties.SetName(Board, Loc.T("board.name"));

        NavGameItem.Content = Loc.T("nav.game");
        NavSettingsItem.Content = Loc.T("nav.settings");
        SettingsView.ApplyLocalization();

        UpdateBestTimeText();
    }

    /// <summary>Applies a language picked in settings and remembers the choice.</summary>
    private void SetLanguage(AppLanguage language)
    {
        if (_applyingLanguage || _settings.Language == language)
        {
            return;
        }

        _applyingLanguage = true;
        try
        {
            _settings.Language = language;
            ApplyLanguage();
            SetStatus(string.Empty, StatusTone.Neutral);
            FitWindowToBoard();
        }
        finally
        {
            _applyingLanguage = false;
        }

        UiSounds.Play(ElementSoundKind.Invoke);
        SaveSettings();
    }

    /// <summary>Pushes the persisted mute flag into both audio layers.</summary>
    private void ApplySoundSetting()
    {
        GameSounds.Enabled = _settings.SoundEnabled;
        UiSounds.Enabled = _settings.SoundEnabled;
    }

    private void ApplyThemeSetting()
    {        RootGrid.RequestedTheme = _settings.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        ApplyCaptionButtonColors();
    }

    private void ApplyCaptionButtonColors()
    {
        bool dark = RootGrid.ActualTheme == ElementTheme.Dark;
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = dark ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 0x1A, 0x1A, 0x1A);
        titleBar.ButtonInactiveForegroundColor = dark
            ? Color.FromArgb(255, 0x9A, 0x9A, 0x9A)
            : Color.FromArgb(255, 0x84, 0x84, 0x84);
        titleBar.ButtonHoverBackgroundColor = dark
            ? Color.FromArgb(0x20, 255, 255, 255)
            : Color.FromArgb(0x14, 0, 0, 0);
        titleBar.ButtonHoverForegroundColor = titleBar.ButtonForegroundColor;
        titleBar.ButtonPressedBackgroundColor = dark
            ? Color.FromArgb(0x30, 255, 255, 255)
            : Color.FromArgb(0x20, 0, 0, 0);
        titleBar.ButtonPressedForegroundColor = titleBar.ButtonForegroundColor;
    }

    // ── 命令 ────────────────────────────────────────────────────────────────────

    private void NewGameButton_Click(object sender, RoutedEventArgs e)
    {
        UiSounds.Play(ElementSoundKind.Invoke);
        GameSounds.Play(GameSound.Click);
        Restart();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        UiSounds.Play(ElementSoundKind.Invoke);
        GameSounds.Play(GameSound.Click);
        Restart();
    }

    private void Restart()
    {
        var preset = _difficulty == DifficultyLevel.Custom
            ? _settings.CustomBoard
            : DifficultyPresets.Get(_difficulty) ?? BoardPreset.Beginner;
        StartNewGame(preset, _difficulty);
    }

    private async void DifficultyBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_suppressDifficultyChanges || !_loaded)
        {
            return;
        }

        if (sender.SelectedItem == ItemCustom)
        {
            await PromptCustomGameAsync();
            return;
        }

        var level = sender.SelectedItem == ItemIntermediate
            ? DifficultyLevel.Intermediate
            : sender.SelectedItem == ItemExpert
                ? DifficultyLevel.Expert
                : DifficultyLevel.Beginner;

        UiSounds.Play(ElementSoundKind.MoveNext);
        StartNewGame(DifficultyPresets.Get(level) ?? BoardPreset.Beginner, level);
    }

    private async Task PromptCustomGameAsync()
    {
        var dialog = new CustomGameDialog(_settings.CustomBoard) { XamlRoot = RootGrid.XamlRoot };
        _modalOpen = true;
        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        finally
        {
            _modalOpen = false;
        }

        if (result == ContentDialogResult.Primary && dialog.Board is { } board)
        {
            StartNewGame(board, DifficultyLevel.Custom);
        }
        else
        {
            SelectDifficulty(_difficulty);
        }
    }

    // ── 导航与设置页 ────────────────────────────────────────────────────────────

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNavChanges)
        {
            return;
        }

        UiSounds.Play(ElementSoundKind.MoveNext);
        ShowPage(IsSettingsContainer(args.SelectedItemContainer));
    }

    private static bool IsSettingsContainer(object? container) =>
        container is NavigationViewItem item && item.Tag as string == "settings";

    /// <summary>Swaps the game page and the settings page.</summary>
    private void ShowPage(bool settings)
    {
        GameView.Visibility = settings ? Visibility.Collapsed : Visibility.Visible;
        SettingsView.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;

        if (settings)
        {
            SettingsView.SetVersion(AppVersion);
            SettingsView.SyncFrom(_settings);
        }
    }

    /// <summary>Selects a page in the navigation view and shows it.</summary>
    private void SelectPage(bool settings)
    {
        _suppressNavChanges = true;
        try
        {
            Nav.SelectedItem = settings ? NavSettingsItem : NavGameItem;
        }
        finally
        {
            _suppressNavChanges = false;
        }

        ShowPage(settings);
    }

    private void SettingsView_ThemeChanged(object? sender, AppTheme theme)
    {
        // Same reasoning as the language handler: switching the theme re-templates the whole window,
        // which must not happen while the ComboBox that raised the event is still closing its popup.
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                _settings.Theme = theme;
                ApplyThemeSetting();
                if (_board is not null)
                {
                    SetStatus(StatusText.Text, StatusTone.Neutral);
                }

                UiSounds.Play(ElementSoundKind.Invoke);
                SaveSettings();
                SettingsView.SyncFrom(_settings);
            }
            catch (Exception ex)
            {
                App.LogCrash(ex);
            }
        });
    }

    private void SettingsView_SoundChanged(object? sender, bool enabled)
    {
        try
        {
            _settings.SoundEnabled = enabled;
            ApplySoundSetting();
            UiSounds.Play(ElementSoundKind.Invoke);
            GameSounds.Play(GameSound.Click);
            SaveSettings();
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
        }
    }

    private void SettingsView_LanguageChanged(object? sender, AppLanguage language)
    {
        // Re-rendering the page and resizing the window from inside the ComboBox's own
        // SelectionChanged callback (the dropdown is still closing at that point) is what used to
        // take the process down, so the work is deferred to the next dispatcher tick.
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                SetLanguage(language);
                SettingsView.SyncFrom(_settings);
            }
            catch (Exception ex)
            {
                // An exception escaping a XAML event handler takes the process down; log and keep going.
                App.LogCrash(ex);
            }
        });
    }

    private async void SettingsView_ResetBestTimesRequested(object? sender, EventArgs e) =>
        await ConfirmResetBestTimesAsync();

    private async void SettingsView_ViewSourceRequested(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/Churroser/winmine"));
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
        }
    }

    private async Task ConfirmResetBestTimesAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = Loc.T("dialog.reset.title"),
            Content = Loc.T("dialog.reset.body"),
            PrimaryButtonText = Loc.T("dialog.reset.primary"),
            CloseButtonText = Loc.T("action.cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        ContentDialogResult answer;
        _modalOpen = true;
        try
        {
            answer = await dialog.ShowAsync();
        }
        finally
        {
            _modalOpen = false;
        }

        if (answer != ContentDialogResult.Primary)
        {
            return;
        }

        _bestTimes.Clear();
        PersistBestTimes();
        UpdateBestTimeText();
        SetStatus(Loc.T("status.bestCleared"), StatusTone.Neutral);
    }

    private static string AppVersion
    {
        get
        {
            Version? version = typeof(App).Assembly.GetName().Version;
            return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    private async Task ShowAboutAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = Loc.T("app.title"),
            Content = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Loc.T("dialog.about.tagline"), TextWrapping = TextWrapping.Wrap },
                    new TextBlock
                    {
                        Text = Loc.T("dialog.about.controls"),
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = SecondaryBrush(),
                    },
                    new TextBlock
                    {
                        Text = Loc.F("dialog.about.version", AppVersion),
                        Foreground = SecondaryBrush(),
                    },
                },
            },
            CloseButtonText = Loc.T("action.close"),
        };

        _modalOpen = true;
        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            _modalOpen = false;
        }
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Esc leaves the settings page, the way Back behaves in a Windows 11 app.
        if (e.Key == VirtualKey.Escape && SettingsView.Visibility == Visibility.Visible)
        {
            SelectPage(false);
            e.Handled = true;
            return;
        }

        bool control = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);

        if (ApplyShortcut(e.Key, control))
        {
            e.Handled = true;
        }
    }

    /// <summary>Window-level shortcuts. Returns true when the key was consumed.</summary>
    private bool ApplyShortcut(VirtualKey key, bool control)
    {
        if (_modalOpen)
        {
            // A dialog owns the keyboard while it is open.
            return false;
        }

        if (key == VirtualKey.F2)
        {
            Restart();
            return true;
        }

        if (!control)
        {
            return false;
        }

        switch (key)
        {
            case VirtualKey.N:
                Restart();
                return true;
            case VirtualKey.Number1:
                StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner);
                return true;
            case VirtualKey.Number2:
                StartNewGame(BoardPreset.Intermediate, DifficultyLevel.Intermediate);
                return true;
            case VirtualKey.Number3:
                StartNewGame(BoardPreset.Expert, DifficultyLevel.Expert);
                return true;
            default:
                return false;
        }
    }
}
