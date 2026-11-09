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
    private static readonly AppTheme[] ThemeOrder = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    private bool _syncing;

    private string _version = "1.0.0";

    public SettingsPage()
    {
        InitializeComponent();
        BuildOptions();
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
        ApplyVersionText();

        UpdateOptionTexts();
    }

    /// <summary>Shows the version under the app name, the way Windows 11 Settings does.</summary>
    public void SetVersion(string version)
    {
        _version = version;
        ApplyVersionText();
    }

    private void ApplyVersionText() => AboutCard.Description = Loc.F("settings.version", _version);

    // Read-only mirrors of the control state, used by --selftest.
    internal AppTheme SelectedTheme => CurrentTag(ThemeCombo, AppTheme.System);

    internal AppLanguage SelectedLanguage => CurrentTag(LanguageCombo, AppLanguage.System);

    internal bool SoundOn => SoundToggle.IsOn;

    internal string ThemeHeader => ThemeCard.Header;

    /// <summary>The subtitle under the app name; carries the localised version string.</summary>
    internal string VersionDescription => AboutCard.Description;

    internal int LanguageOptionCount => LanguageCombo.Items.Count;

    /// <summary>What the closed theme box currently renders (the ComboBox snapshots this).</summary>
    internal string ThemeSelectionText => ThemeCombo.SelectionBoxItem as string ?? string.Empty;

    /// <summary>What the closed language box currently renders.</summary>
    internal string LanguageSelectionText => LanguageCombo.SelectionBoxItem as string ?? string.Empty;

    /// <summary>True when both closed boxes show the text of the item that is actually selected.</summary>
    internal bool SelectionTextsAreCurrent =>
        SelectionTextMatches(ThemeCombo) && SelectionTextMatches(LanguageCombo);

    private static bool SelectionTextMatches(ComboBox combo) =>
        combo.SelectedItem is ComboBoxItem row
        && Equals(combo.SelectionBoxItem as string, row.Content as string);

    /// <summary>Simulates a real pick in the language dropdown (used by --selftest).</summary>
    internal bool SelectLanguageForTest(AppLanguage language)
    {
        foreach (object item in LanguageCombo.Items)
        {
            if (item is ComboBoxItem row && row.Tag is AppLanguage tag && tag == language)
            {
                LanguageCombo.SelectedItem = row;
                return true;
            }
        }

        return false;
    }

    /// <summary>Opens the language popup so a test can select while the item containers exist.</summary>
    internal void SetLanguageDropDownForTest(bool open) => LanguageCombo.IsDropDownOpen = open;

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

    /// <summary>
    /// Creates the combo items once. They are never cleared afterwards: emptying a ComboBox's item
    /// collection from inside its own SelectionChanged handler (the language switch re-renders the
    /// page) tears down the item container while the popup is still closing and crashes the app.
    /// </summary>
    private void BuildOptions()
    {
        _syncing = true;
        try
        {
            foreach (AppTheme theme in ThemeOrder)
            {
                ThemeCombo.Items.Add(Option(string.Empty, theme));
            }

            LanguageCombo.Items.Add(Option(string.Empty, AppLanguage.System));
            foreach (AppLanguage candidate in LanguageResolver.Supported)
            {
                LanguageCombo.Items.Add(Option(string.Empty, candidate));
            }

            ThemeCombo.SelectedIndex = 0;
            LanguageCombo.SelectedIndex = 0;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Retranslates the existing items in place; the item objects (and the selection) stay put.</summary>
    private void UpdateOptionTexts()
    {
        foreach (object item in ThemeCombo.Items)
        {
            if (item is ComboBoxItem row && row.Tag is AppTheme theme)
            {
                row.Content = ThemeLabel(theme);
            }
        }

        foreach (object item in LanguageCombo.Items)
        {
            if (item is ComboBoxItem row && row.Tag is AppLanguage language)
            {
                row.Content = language == AppLanguage.System
                    ? Loc.T("menu.languageSystem")
                    : LanguageResolver.NativeName(language);
            }
        }

        RefreshSelectionBox(ThemeCombo);
        RefreshSelectionBox(LanguageCombo);
    }

    /// <summary>
    /// A ComboBox snapshots the selected item's content into <c>SelectionBoxItem</c> when the selection
    /// changes, so editing the item text alone leaves the closed box showing the old language. Drop the
    /// selection and put it back to make the box re-render — never while the pop-up is open, because the
    /// item containers are live then.
    /// </summary>
    private void RefreshSelectionBox(ComboBox combo)
    {
        if (combo.IsDropDownOpen || combo.SelectedItem is not ComboBoxItem selected)
        {
            return;
        }

        bool previous = _syncing;
        _syncing = true;
        try
        {
            combo.SelectedItem = null;
            combo.SelectedItem = selected;
        }
        finally
        {
            _syncing = previous;
        }
    }

    private static string ThemeLabel(AppTheme theme) => theme switch
    {
        AppTheme.Light => Loc.T("menu.themeLight"),
        AppTheme.Dark => Loc.T("menu.themeDark"),
        _ => Loc.T("menu.themeSystem"),
    };

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

    /// <summary>
    /// A language switch that happens while the popup is open cannot re-render the closed box yet
    /// (its containers are live); once the popup closes we can safely re-select and re-render.
    /// </summary>
    private void Combo_DropDownClosed(object sender, object e)
    {
        if (sender is ComboBox combo)
        {
            RefreshSelectionBox(combo);
        }
    }

    private void SoundToggle_Toggled(object sender, RoutedEventArgs e)    {
        if (_syncing)
        {
            return;
        }

        SoundChanged?.Invoke(this, SoundToggle.IsOn);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e) => ResetBestTimesRequested?.Invoke(this, EventArgs.Empty);

    private void SourceLink_Click(object sender, RoutedEventArgs e) => ViewSourceRequested?.Invoke(this, EventArgs.Empty);
}
