using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinMine.Core;
using WinMine.Services;

namespace WinMine.Views;

/// <summary>
/// The Settings page: a Windows 11 style list of <see cref="Controls.SettingsCard"/> rows grouped
/// into Appearance / Sound / Language / Data / About. The page only reports what the user picked;
/// <see cref="MainWindow"/> owns the settings object and decides what to persist.
/// </summary>
public sealed partial class SettingsPage : UserControl
{
    private bool _syncing;

    public SettingsPage()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    /// <summary>Raised when the user picks another theme.</summary>
    public event EventHandler<AppTheme>? ThemeChanged;

    /// <summary>Raised when the user flips the sound switch.</summary>
    public event EventHandler<bool>? SoundChanged;

    /// <summary>Raised when the user picks another UI language.</summary>
    public event EventHandler<AppLanguage>? LanguageChanged;

    /// <summary>Raised when the user asks to clear every best time.</summary>
    public event EventHandler? ResetBestTimesRequested;

    /// <summary>Raised when the user opens the repository.</summary>
    public event EventHandler? ViewSourceRequested;

    /// <summary>Re-renders every visible string in the page's current language.</summary>
    public void ApplyLocalization()
    {
        PageTitle.Text = Loc.T("settings.title");
        AppearanceHeader.Text = Loc.T("settings.appearance");
        SoundHeader.Text = Loc.T("settings.sound");
        LanguageHeader.Text = Loc.T("settings.language");
        DataHeader.Text = Loc.T("settings.data");
        AboutHeader.Text = Loc.T("settings.aboutTitle");

        ThemeCard.Header = Loc.T("settings.theme");
        ThemeCard.Description = Loc.T("settings.themeDesc");
        SoundCard.Header = Loc.T("settings.sound");
        SoundCard.Description = Loc.T("settings.soundDesc");
        LanguageCard.Header = Loc.T("settings.language");
        LanguageCard.Description = Loc.T("settings.languageDesc");
        ResetCard.Header = Loc.T("settings.resetTitle");
        ResetCard.Description = Loc.T("settings.resetDesc");
        ResetButton.Content = Loc.T("settings.resetButton");
        AboutCard.Header = Loc.T("app.title");
        SourceLink.Content = Loc.T("settings.viewSource");

        RefreshOptions();
    }

    /// <summary>Shows the version next to the About row.</summary>
    public void SetVersion(string version) => VersionText.Text = Loc.F("settings.version", version);

    // Read-only mirrors of the control state, used by --selftest.
    internal AppTheme SelectedTheme => CurrentTag(ThemeCombo, AppTheme.System);

    internal AppLanguage SelectedLanguage => CurrentTag(LanguageCombo, AppLanguage.System);

    internal bool SoundOn => SoundToggle.IsOn;

    internal string ThemeHeader => ThemeCard.Header;

    internal int LanguageOptionCount => LanguageCombo.Items.Count;

    /// <summary>Pushes the persisted settings into the controls without raising change events.</summary>
    public void SyncFrom(GameSettings settings)
    {
        _syncing = true;
        try
        {
            SelectByTag(ThemeCombo, settings.Theme);
            SelectByTag(LanguageCombo, settings.Language);
            SoundToggle.IsOn = settings.SoundEnabled;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Rebuilds the combo items so that their text follows the current language.</summary>
    private void RefreshOptions()
    {
        AppTheme theme = CurrentTag(ThemeCombo, AppTheme.System);
        AppLanguage language = CurrentTag(LanguageCombo, AppLanguage.System);

        _syncing = true;
        try
        {
            ThemeCombo.Items.Clear();
            ThemeCombo.Items.Add(Option(Loc.T("menu.themeSystem"), AppTheme.System));
            ThemeCombo.Items.Add(Option(Loc.T("menu.themeLight"), AppTheme.Light));
            ThemeCombo.Items.Add(Option(Loc.T("menu.themeDark"), AppTheme.Dark));

            LanguageCombo.Items.Clear();
            LanguageCombo.Items.Add(Option(Loc.T("menu.languageSystem"), AppLanguage.System));
            foreach (AppLanguage candidate in LanguageResolver.Supported)
            {
                LanguageCombo.Items.Add(Option(LanguageResolver.NativeName(candidate), candidate));
            }

            SelectByTag(ThemeCombo, theme);
            SelectByTag(LanguageCombo, language);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static ComboBoxItem Option(string text, object tag) => new() { Content = text, Tag = tag };

    private static void SelectByTag(ComboBox combo, object tag)
    {
        foreach (object item in combo.Items)
        {
            if (item is ComboBoxItem row && Equals(row.Tag, tag))
            {
                combo.SelectedItem = row;
                return;
            }
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private static T CurrentTag<T>(ComboBox combo, T fallback) where T : struct, Enum =>
        combo.SelectedItem is ComboBoxItem row && row.Tag is T value ? value : fallback;

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ThemeCombo.SelectedItem is not ComboBoxItem row || row.Tag is not AppTheme theme)
        {
            return;
        }

        ThemeChanged?.Invoke(this, theme);
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || LanguageCombo.SelectedItem is not ComboBoxItem row || row.Tag is not AppLanguage language)
        {
            return;
        }

        LanguageChanged?.Invoke(this, language);
    }

    private void SoundToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        SoundChanged?.Invoke(this, SoundToggle.IsOn);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e) => ResetBestTimesRequested?.Invoke(this, EventArgs.Empty);

    private void SourceLink_Click(object sender, RoutedEventArgs e) => ViewSourceRequested?.Invoke(this, EventArgs.Empty);
}
