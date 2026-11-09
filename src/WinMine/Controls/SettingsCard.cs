using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinMine.Controls;

/// <summary>
/// A Windows 11 Settings-style row: icon, title, one-line description and a control on the right.
/// Content is what appears in the right-hand slot, so <c>&lt;controls:SettingsCard&gt;...&lt;/controls:SettingsCard&gt;</c>
/// holds the switch or combo box.
/// </summary>
public sealed class SettingsCard : ContentControl
{
    public SettingsCard()
    {
        DefaultStyleKey = typeof(SettingsCard);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Right;
        VerticalContentAlignment = VerticalAlignment.Center;
    }

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    /// <summary>Segoe Fluent Icons code point rendered on the left.</summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
