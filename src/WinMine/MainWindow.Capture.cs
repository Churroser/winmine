using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinMine.Core;
using WinMine.Views;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace WinMine;

/// <summary>
/// Diagnostic mode (<c>WinMine.exe --capture DIR</c>) used to verify the real WinUI layout:
/// it scripts the game into known states and renders the live visual tree to PNG.
/// </summary>
public sealed partial class MainWindow
{
    private string? _captureDirectory;

    private async Task RunCaptureAsync()
    {
        string directory = _captureDirectory!;
        try
        {
            Directory.CreateDirectory(directory);
            await Task.Delay(700);
            await CaptureScenariosAsync(directory);
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "capture-error.txt"), ex.ToString());
            }
            catch (IOException)
            {
            }
        }
        finally
        {
            await Task.Delay(200);
            Application.Current.Exit();
        }
    }

    private async Task CaptureScenariosAsync(string directory)
    {
        // --lang picks the base language of the whole set, so the docs can ship one screenshot
        // folder per README language. Defaults to Simplified Chinese.
        AppLanguage baseline = App.CaptureLanguageOverride ?? AppLanguage.ChineseSimplified;
        CaptureLanguage(baseline);

        // 1 · 初级 · 浅色 · 刚开局
        SetCaptureTheme(AppTheme.Light);
        StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 20240607);
        await Task.Delay(450);
        await SaveShotAsync(Path.Combine(directory, "01-beginner-new-light.png"));

        // 2 · 初级 · 浅色 · 进行中（翻开 + 插旗）
        PlayDemo(new Cell(4, 4), seed: 20240607, reveals: 7, flags: 2);
        await Task.Delay(1500);
        await SaveShotAsync(Path.Combine(directory, "02-beginner-playing-light.png"));

        // 3 · 初级 · 浅色 · 通关
        StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 777);
        Board.RevealAt(new Cell(4, 4));
        await Task.Delay(1700);
        RevealAllSafe();
        await Task.Delay(650);
        await SaveShotAsync(Path.Combine(directory, "03-beginner-won-light.png"));

        // 4 · 初级 · 浅色 · 踩雷
        StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 4242);
        Board.RevealAt(new Cell(4, 4));
        await Task.Delay(1300);
        var mine = FirstMine();
        if (mine is { } bad)
        {
            Board.RevealAt(bad);
        }

        await Task.Delay(650);
        await SaveShotAsync(Path.Combine(directory, "04-beginner-lost-light.png"));

        // 5 · 中级 · 深色 · 进行中
        SetCaptureTheme(AppTheme.Dark);
        StartNewGame(BoardPreset.Intermediate, DifficultyLevel.Intermediate, 606);
        PlayDemo(new Cell(8, 8), seed: 606, reveals: 10, flags: 4);
        await Task.Delay(2400);
        await SaveShotAsync(Path.Combine(directory, "05-intermediate-playing-dark.png"));

        // 6 · 高级 · 深色 · 进行中
        StartNewGame(BoardPreset.Expert, DifficultyLevel.Expert, 99);
        PlayDemo(new Cell(8, 15), seed: 99, reveals: 14, flags: 6);
        await Task.Delay(3100);
        await SaveShotAsync(Path.Combine(directory, "06-expert-playing-dark.png"));

        // 7 · 初级 · 深色 · 数字配色
        StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 31337);
        PlayDemo(new Cell(4, 4), seed: 31337, reveals: 4, flags: 2);
        await Task.Delay(1350);
        await SaveShotAsync(Path.Combine(directory, "07-beginner-playing-dark.png"));

        // 8 · 自定义棋盘对话框 · 浅色
        SetCaptureTheme(AppTheme.Light);
        StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 20240607);
        await Task.Delay(300);
        await CaptureCustomDialogAsync(Path.Combine(directory, "08-custom-dialog-light.png"));

        // 9–12 · 多语言：同一局面，另外四种界面语言（不含当前基准语言）
        var localizedShots = new[]
            {
                (AppLanguage.Japanese, AppTheme.Light),
                (AppLanguage.English, AppTheme.Light),
                (AppLanguage.ChineseSimplified, AppTheme.Light),
                (AppLanguage.French, AppTheme.Dark),
                (AppLanguage.Korean, AppTheme.Light),
            }
            .Where(shot => shot.Item1 != baseline)
            .ToArray();

        for (int i = 0; i < localizedShots.Length; i++)
        {
            var (language, theme) = localizedShots[i];
            SetCaptureTheme(theme);
            CaptureLanguage(language);
            StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 20240607);
            PlayDemo(new Cell(4, 4), seed: 20240607, reveals: 5, flags: 2);
            await Task.Delay(1400);
            await SaveShotAsync(Path.Combine(directory, $"{9 + i:00}-localized-{LanguageShotName(language)}.png"));
        }

        CaptureLanguage(baseline);

        // 13–14 · 设置页（浅色 / 深色）
        SetCaptureTheme(AppTheme.Light);
        SelectPage(true);
        SettingsView.SyncFrom(_settings);
        await Task.Delay(900);
        await SaveShotAsync(Path.Combine(directory, "13-settings-light.png"));

        SetCaptureTheme(AppTheme.Dark);
        SettingsView.SyncFrom(_settings);
        await Task.Delay(700);
        await SaveShotAsync(Path.Combine(directory, "14-settings-dark.png"));

        SelectPage(false);
        await Task.Delay(200);

        // 15 · chord preview: hold on a revealed number and its hidden neighbours sink in
        SetCaptureTheme(AppTheme.Light);
        Cell? previewOrigin = null;
        foreach (int reveals in new[] { 10, 8, 6, 4, 3 })
        {
            StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 20240607);
            PlayDemo(new Cell(4, 4), seed: 20240607, reveals: reveals, flags: 2);
            await Task.Delay(650);
            if (Board.Board?.Status is GameStatus.Playing && FindChordPreviewOrigin() is { } candidate)
            {
                previewOrigin = candidate;
                break;
            }
        }

        if (previewOrigin is { } origin)
        {
            Board.PreviewChordForTest(origin);
            await Task.Delay(260);
            await SaveShotAsync(Path.Combine(directory, "15-chord-preview.png"));
            Board.ClearChordPreviewForTest();
        }

        await Task.Delay(120);
    }

    /// <summary>The revealed number with the most hidden neighbours — what a chord would open.</summary>
    private Cell? FindChordPreviewOrigin()
    {
        var grid = Board.Board;
        if (grid is null)
        {
            return null;
        }

        Cell? best = null;
        int bestCount = 0;
        foreach (var cell in grid.AllCells())
        {
            var info = grid.GetCell(cell);
            if (!info.IsRevealed || info.AdjacentMines == 0)
            {
                continue;
            }

            int hidden = grid.Neighbours(cell)
                .Count(n => !grid.GetCell(n).IsRevealed && !grid.GetCell(n).IsFlagged);
            if (hidden > bestCount)
            {
                best = cell;
                bestCount = hidden;
            }
        }

        return best;
    }

    /// <summary>File-name fragment for a language's screenshot (English, not "english").</summary>
    private static string LanguageShotName(AppLanguage language) => language switch
    {
        AppLanguage.Japanese => "japanese",
        AppLanguage.ChineseSimplified => "chinese",
        AppLanguage.French => "french",
        AppLanguage.Korean => "korean",
        _ => "english",
    };

    /// <summary>Switches the whole window to <paramref name="language"/> for a screenshot.</summary>
    private void CaptureLanguage(AppLanguage language)
    {
        _settings.Language = language;
        ApplyLanguage();
    }

    private async Task CaptureCustomDialogAsync(string path)
    {
        // The dialog body is rendered inside a plain host instead of the live ContentDialog:
        // a dialog lives in the popup layer, which RenderTargetBitmap cannot capture, and the
        // entrance animation would otherwise leave the shot clipped.
        var dialog = new CustomGameDialog(_settings.CustomBoard);
        if (dialog.Content is not UIElement content)
        {
            return;
        }

        dialog.Content = null;

        var host = new Border
        {
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(255, 0xF9, 0xF9, 0xF9)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0xE0, 0xE0, 0xE0)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = content,
        };

        // Overlay straight on the root grid: ContentHost is a NavigationView now, and the dialog
        // body has to sit above the page content anyway.
        Grid.SetRow(host, 1);
        RootGrid.Children.Add(host);
        try
        {
            await Task.Delay(400);
            await SaveElementShotAsync(path, host);
        }
        catch (Exception ex)
        {
            App.LogCrash(ex);
        }
        finally
        {
            RootGrid.Children.Remove(host);
            await Task.Delay(100);
        }
    }

    private void PlayDemo(Cell center, int seed, int reveals, int flags)
    {
        if (Board.Board is null)
        {
            return;
        }

        Board.RevealAt(center);
        var random = new Random(seed);
        for (int i = 0; i < reveals; i++)
        {
            if (Board.Board.Status is GameStatus.Won or GameStatus.Lost)
            {
                break;
            }

            var candidates = HiddenSafeCells();
            if (candidates.Count == 0)
            {
                break;
            }

            Board.RevealAt(candidates[random.Next(candidates.Count)]);
        }

        if (Board.Board.Status is GameStatus.Won or GameStatus.Lost)
        {
            return;
        }

        foreach (var mineCell in Board.Board.MinePositions().Take(flags))
        {
            Board.FlagAt(mineCell);
        }
    }

    private void RevealAllSafe()
    {
        if (Board.Board is null)
        {
            return;
        }

        for (int r = 0; r < Board.Board.Rows; r++)
        {
            for (int c = 0; c < Board.Board.Columns; c++)
            {
                var cell = new Cell(r, c);
                if (Board.Board.Status is GameStatus.Won or GameStatus.Lost)
                {
                    return;
                }

                if (!Board.Board.IsMineAt(cell) && !Board.Board.GetCell(cell).IsRevealed)
                {
                    Board.RevealAt(cell);
                }
            }
        }
    }

    private List<Cell> HiddenSafeCells()
    {
        var list = new List<Cell>();
        if (Board.Board is null)
        {
            return list;
        }

        for (int r = 0; r < Board.Board.Rows; r++)
        {
            for (int c = 0; c < Board.Board.Columns; c++)
            {
                var cell = new Cell(r, c);
                var info = Board.Board.GetCell(cell);
                if (!info.IsRevealed && !info.IsFlagged && !Board.Board.IsMineAt(cell))
                {
                    list.Add(cell);
                }
            }
        }

        return list;
    }

    private Cell? FirstMine()
    {
        if (Board.Board is null)
        {
            return null;
        }

        foreach (var cell in Board.Board.MinePositions())
        {
            if (!Board.Board.GetCell(cell).IsRevealed)
            {
                return cell;
            }
        }

        return null;
    }

    private void SetCaptureTheme(AppTheme theme)
    {
        _settings.Theme = theme;
        RootGrid.RequestedTheme = theme == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        RootGrid.Background = new SolidColorBrush(theme == AppTheme.Dark
            ? Color.FromArgb(255, 0x20, 0x20, 0x20)
            : Color.FromArgb(255, 0xF3, 0xF3, 0xF3));
        ApplyCaptionButtonColors();
    }

    private async Task SaveShotAsync(string path)
    {
        await SaveElementShotAsync(path, RootGrid);
    }

    private static async Task SaveElementShotAsync(string path, UIElement element)
    {
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element);
        var pixels = await bitmap.GetPixelsAsync();

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight,
            96,
            96,
            pixels.ToArray());
        await encoder.FlushAsync();

        stream.Seek(0);
        var bytes = new byte[stream.Size];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(bytes);
        await File.WriteAllBytesAsync(path, bytes);
    }
}
