using System.Globalization;
using WinMine.Core;
using Xunit;

namespace WinMine.Core.Tests;

public sealed class LanguageTests
{
    [Theory]
    [InlineData("zh-CN", AppLanguage.ChineseSimplified)]
    [InlineData("zh-TW", AppLanguage.ChineseSimplified)]
    [InlineData("zh-Hans-CN", AppLanguage.ChineseSimplified)]
    [InlineData("ja-JP", AppLanguage.Japanese)]
    [InlineData("fr-FR", AppLanguage.French)]
    [InlineData("fr-CA", AppLanguage.French)]
    [InlineData("ko-KR", AppLanguage.Korean)]
    [InlineData("en-US", AppLanguage.English)]
    [InlineData("en-GB", AppLanguage.English)]
    [InlineData("de-DE", AppLanguage.English)]
    [InlineData("ru-RU", AppLanguage.English)]
    public void CulturesMapOntoShippedLanguages(string cultureName, AppLanguage expected)
    {
        Assert.Equal(expected, LanguageResolver.FromCulture(new CultureInfo(cultureName)));
    }

    [Fact]
    public void InvariantAndNullCultureFallBackToEnglish()
    {
        Assert.Equal(AppLanguage.English, LanguageResolver.FromCulture(CultureInfo.InvariantCulture));
        Assert.Equal(AppLanguage.English, LanguageResolver.FromCulture(null));
        Assert.Equal(AppLanguage.English, LanguageResolver.FromIsoLanguage(" "));
        Assert.Equal(AppLanguage.English, LanguageResolver.FromIsoLanguage(null));
    }

    [Fact]
    public void IsoLanguageCodesAreCaseInsensitive()
    {
        Assert.Equal(AppLanguage.Japanese, LanguageResolver.FromIsoLanguage("JA"));
        Assert.Equal(AppLanguage.Korean, LanguageResolver.FromIsoLanguage("Ko"));
    }

    [Fact]
    public void SystemSettingResolvesThroughTheUiCulture()
    {
        Assert.Equal(AppLanguage.Japanese, LanguageResolver.Resolve(AppLanguage.System, new CultureInfo("ja-JP")));
        Assert.Equal(AppLanguage.French, LanguageResolver.Resolve(AppLanguage.French, new CultureInfo("ja-JP")));
    }

    [Fact]
    public void SupportedListHoldsFiveDistinctLanguages()
    {
        Assert.Equal(5, LanguageResolver.Supported.Count);
        Assert.Equal(LanguageResolver.Supported.Count, LanguageResolver.Supported.Distinct().Count());
        Assert.DoesNotContain(AppLanguage.System, LanguageResolver.Supported);
    }

    [Fact]
    public void EveryLanguageHasAUniqueTagAndNativeName()
    {
        var languages = Enum.GetValues<AppLanguage>();
        var tags = languages.Select(LanguageResolver.Tag).ToArray();
        var names = languages.Select(LanguageResolver.NativeName).ToArray();

        Assert.Equal(languages.Length, tags.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(languages.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("Japanese", AppLanguage.Japanese)]
    [InlineData("french", AppLanguage.French)]
    [InlineData("ko", AppLanguage.Korean)]
    [InlineData("zh-Hans", AppLanguage.ChineseSimplified)]
    [InlineData("System", AppLanguage.System)]
    public void TryParseAcceptsNamesAndTags(string text, AppLanguage expected)
    {
        Assert.True(LanguageResolver.TryParse(text, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void TryParseRejectsUnknownText()
    {
        Assert.False(LanguageResolver.TryParse("klingon", out var parsed));
        Assert.False(LanguageResolver.TryParse(null, out _));
        Assert.Equal(AppLanguage.System, parsed);
    }

    [Fact]
    public void SettingsPersistTheLanguageChoice()
    {
        var settings = new GameSettings { Language = AppLanguage.Korean };
        string json = settings.ToJson();

        Assert.Contains("\"Korean\"", json, StringComparison.Ordinal);
        Assert.Equal(AppLanguage.Korean, GameSettings.FromJson(json).Language);
    }

    [Fact]
    public void SettingsDefaultToFollowingTheSystem()
    {
        Assert.Equal(AppLanguage.System, new GameSettings().Language);
        Assert.Equal(AppLanguage.System, GameSettings.FromJson(null).Language);
        Assert.Equal(AppLanguage.System, GameSettings.FromJson("{ \"Language\": 42 }").Language);
    }

    [Fact]
    public void BoardValidationReportsTheReason()
    {
        Assert.True(Minefield.TryValidate(9, 9, 10, out BoardValidationError ok));
        Assert.Equal(BoardValidationError.None, ok);

        Assert.False(Minefield.TryValidate(2, 9, 10, out BoardValidationError rows));
        Assert.Equal(BoardValidationError.RowsOutOfRange, rows);

        Assert.False(Minefield.TryValidate(9, 41, 10, out BoardValidationError columns));
        Assert.Equal(BoardValidationError.ColumnsOutOfRange, columns);

        Assert.False(Minefield.TryValidate(9, 9, 81, out BoardValidationError mines));
        Assert.Equal(BoardValidationError.MinesOutOfRange, mines);

        Assert.False(Minefield.TryValidate(9, 9, 0, out BoardValidationError zeroMines));
        Assert.Equal(BoardValidationError.MinesOutOfRange, zeroMines);
    }

    [Fact]
    public void StringValidationStillReturnsChineseText()
    {
        Assert.False(Minefield.IsValid(2, 9, 10, out string? error));
        Assert.NotNull(error);
        Assert.Contains("行数", error!, StringComparison.Ordinal);

        Assert.True(Minefield.IsValid(9, 9, 10, out string? none));
        Assert.Null(none);
    }
}
