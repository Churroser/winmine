namespace WinMine.Core;

using System.Globalization;

/// <summary>
/// UI language of the app. <see cref="System"/> means "follow the operating system's display
/// language", which is the default; everything else is an explicit override.
/// </summary>
public enum AppLanguage
{
    System = 0,
    ChineseSimplified = 1,
    English = 2,
    Japanese = 3,
    French = 4,
    Korean = 5,
}

/// <summary>
/// Maps cultures to the languages the app actually ships, and resolves the user's
/// <see cref="AppLanguage.System"/> choice to a concrete language.
/// </summary>
public static class LanguageResolver
{
    /// <summary>Languages with a full string table, in menu order.</summary>
    public static IReadOnlyList<AppLanguage> Supported { get; } = new[]
    {
        AppLanguage.ChineseSimplified,
        AppLanguage.English,
        AppLanguage.Japanese,
        AppLanguage.French,
        AppLanguage.Korean,
    };

    /// <summary>Used for any culture the app has no table for.</summary>
    public const AppLanguage DefaultLanguage = AppLanguage.English;

    /// <summary>BCP-47-ish tag persisted in settings, e.g. <c>zh-Hans</c>.</summary>
    public static string Tag(AppLanguage language) => language switch
    {
        AppLanguage.ChineseSimplified => "zh-Hans",
        AppLanguage.Japanese => "ja",
        AppLanguage.French => "fr",
        AppLanguage.Korean => "ko",
        AppLanguage.English => "en",
        _ => "system",
    };

    /// <summary>Endonym, shown in the language menu in its own language whatever the current UI language is.</summary>
    public static string NativeName(AppLanguage language) => language switch
    {
        AppLanguage.ChineseSimplified => "简体中文",
        AppLanguage.Japanese => "日本語",
        AppLanguage.French => "Français",
        AppLanguage.Korean => "한국어",
        AppLanguage.English => "English",
        _ => "System",
    };

    /// <summary>
    /// Maps a culture onto a shipped language. Only the language part matters, so <c>fr-CA</c>
    /// lands on French; every Chinese variant (including <c>zh-Hant</c>, which has no separate
    /// table) lands on Simplified Chinese; anything unknown falls back to English.
    /// </summary>
    public static AppLanguage FromCulture(CultureInfo? culture) =>
        culture is null ? DefaultLanguage : FromIsoLanguage(culture.TwoLetterISOLanguageName);

    public static AppLanguage FromIsoLanguage(string? twoLetterCode) => (twoLetterCode ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "zh" => AppLanguage.ChineseSimplified,
        "ja" => AppLanguage.Japanese,
        "fr" => AppLanguage.French,
        "ko" => AppLanguage.Korean,
        "en" => AppLanguage.English,
        _ => DefaultLanguage,
    };

    /// <summary>Turns the persisted setting into the language to render, consulting the UI culture for <see cref="AppLanguage.System"/>.</summary>
    public static AppLanguage Resolve(AppLanguage setting, CultureInfo? uiCulture = null) =>
        setting == AppLanguage.System
            ? FromCulture(uiCulture ?? CultureInfo.CurrentUICulture)
            : setting;

    public static bool TryParse(string? name, out AppLanguage language)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            foreach (var candidate in Enum.GetValues<AppLanguage>())
            {
                if (string.Equals(candidate.ToString(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    language = candidate;
                    return true;
                }
            }

            string trimmed = name.Trim();
            foreach (var candidate in Enum.GetValues<AppLanguage>())
            {
                if (string.Equals(Tag(candidate), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    language = candidate;
                    return true;
                }
            }
        }

        language = AppLanguage.System;
        return false;
    }
}
