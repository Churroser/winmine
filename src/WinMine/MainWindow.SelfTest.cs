using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using Windows.System;
using WinMine.Core;
using WinMine.Services;

namespace WinMine;

/// <summary>
/// Diagnostic mode (<c>WinMine.exe --selftest FILE</c>): drives the real window, the real board
/// control and the real game loop through scripted interactions and writes a PASS/FAIL report.
/// This is the automated acceptance test that runs without a visible desktop.
/// </summary>
public sealed partial class MainWindow
{
    private bool _selfTestMode;

    private async Task RunSelfTestAsync(string reportPath)
    {
        var lines = new List<string>();
        int failures = 0;
        int total = 0;

        void Check(string name, bool ok, string detail = "")
        {
            total++;
            if (!ok)
            {
                failures++;
            }

            lines.Add($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length == 0 ? string.Empty : "   [" + detail + "]")}");
        }

        MoveResult? last = null;
        Board.MoveCompleted += (_, result) => last = result;

        try
        {
            // ── 1 · 初始状态 ────────────────────────────────────────────────────
            StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 1234);
            await Task.Delay(120);
            Check("棋盘生成 81 个格子控件", Board.CellControlCount == 81, $"count={Board.CellControlCount}");
            Check("计数板初始为雷数", MineCounterText.Text == "010", MineCounterText.Text);
            Check("计时器初始为 000", TimerText.Text == "000", TimerText.Text);
            Check("难度选择器停在初级", DifficultyBar.SelectedItem == ItemBeginner, DifficultyBar.SelectedItem?.Text ?? "null");
            Check("状态栏初始为空", StatusText.Text.Length == 0, StatusText.Text);
            Check("表情为微笑", FaceText.Text == "🙂", FaceText.Text);
            Check("棋盘可获得键盘焦点", Board.Focus(FocusState.Programmatic), "Focus");

            // ── 2 · 第一次点击永远安全 ──────────────────────────────────────────
            Board.RevealAt(new Cell(4, 4));
            await Task.Delay(60);
            var board = _board!;
            Check("首点不踩雷", board.Status != GameStatus.Lost, board.Status.ToString());
            Check("首点触发开局", board.Status == GameStatus.Playing, board.Status.ToString());
            Check("首点 3x3 保护区全部翻开", board.RevealedCount >= 9, $"revealed={board.RevealedCount}");
            Check("首点被记录为 Revealed", last is { Accepted: true, Outcome: MoveOutcome.Revealed }, last?.Outcome.ToString() ?? "null");

            // ── 3 · 插旗 / 取消插旗 ────────────────────────────────────────────
            var flagTarget = FirstHiddenSafeCell() ?? new Cell(0, 0);
            Board.FlagAt(flagTarget);
            await Task.Delay(40);
            Check("右键插旗后旗数 +1", board.FlagCount == 1, $"flags={board.FlagCount}");
            Check("插旗后计数板 -1", MineCounterText.Text == "009", MineCounterText.Text);
            Board.FlagAt(flagTarget);
            await Task.Delay(40);
            Check("再次右键取消插旗", board.FlagCount == 0, $"flags={board.FlagCount}");
            Check("取消后计数板复原", MineCounterText.Text == "010", MineCounterText.Text);

            // ── 4 · 已翻开格不能被插旗 ─────────────────────────────────────────
            int flagsBefore = board.FlagCount;
            Board.FlagAt(new Cell(4, 4));
            await Task.Delay(30);
            Check("已翻开格拒绝插旗", board.FlagCount == flagsBefore, $"flags={board.FlagCount}");

            // ── 5 · 快速展开（chord）────────────────────────────────────────────
            Cell? chordCell = null;
            foreach (var cell in board.RevealedCells())
            {
                var info = board.GetCell(cell);
                if (info.AdjacentMines == 0)
                {
                    continue;
                }

                // A useful chord target needs at least one still-hidden safe neighbour.
                bool hasHiddenSafe = false;
                foreach (var neighbour in board.Neighbours(cell))
                {
                    var neighbourInfo = board.GetCell(neighbour);
                    if (!neighbourInfo.IsRevealed && !board.IsMineAt(neighbour))
                    {
                        hasHiddenSafe = true;
                    }
                }

                if (hasHiddenSafe)
                {
                    chordCell = cell;
                    break;
                }
            }

            if (chordCell is { } target)
            {
                int flaggedMines = 0;
                foreach (var neighbour in board.Neighbours(target))
                {
                    if (board.IsMineAt(neighbour) && !board.GetCell(neighbour).IsFlagged)
                    {
                        Board.FlagAt(neighbour);
                        flaggedMines++;
                    }
                }

                int revealedBefore = board.RevealedCount;
                await Task.Delay(60);
                Board.ChordAt(target);
                await Task.Delay(60);
                Check("旗数正确时快速展开生效", board.RevealedCount > revealedBefore,
                    $"before={revealedBefore} after={board.RevealedCount} flags={flaggedMines}");
                Check("快速展开不会误踩雷", board.Status != GameStatus.Lost, board.Status.ToString());
            }
            else
            {
                Check("找到可测试的快速展开目标", false, "no numbered cell");
            }

            // ── 6 · 通关流程 ────────────────────────────────────────────────────
            StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 777);
            await Task.Delay(80);
            Board.RevealAt(new Cell(4, 4));
            await Task.Delay(1200);
            Check("游戏进行中计时器走动", TimerText.Text != "000", TimerText.Text);
            RevealAllSafeCells();
            await Task.Delay(120);
            board = _board!;
            Check("翻开所有安全格后判定胜利", board.Status == GameStatus.Won, board.Status.ToString());
            Check("胜利后自动为全部雷插旗", board.FlagCount == board.MineCount, $"flags={board.FlagCount}/{board.MineCount}");
            Check("胜利后计数板归零", MineCounterText.Text == "000", MineCounterText.Text);
            Check("胜利表情为墨镜", FaceText.Text == "😎", FaceText.Text);
            Check("胜利状态文案正确", StatusText.Text.StartsWith(LocPrefix("status.won"), StringComparison.Ordinal)
                || StatusText.Text.StartsWith(LocPrefix("status.newRecord"), StringComparison.Ordinal), StatusText.Text);
            Check("胜利后记录最佳成绩", BestTimeText.Text.StartsWith(LocPrefix("footer.best"), StringComparison.Ordinal)
                || BestTimeText.Text.Contains(Loc.T("footer.noBest").Split(' ')[^1], StringComparison.Ordinal), BestTimeText.Text);

            // ── 7 · 失败流程 ────────────────────────────────────────────────────
            StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 4242);
            await Task.Delay(80);
            Board.RevealAt(new Cell(4, 4));
            await Task.Delay(80);
            var mine = _board!.MinePositions().First();
            Board.RevealAt(mine);
            await Task.Delay(120);
            board = _board!;
            Check("踩雷后判定失败", board.Status == GameStatus.Lost, board.Status.ToString());
            Check("失败后揭示全部地雷", board.MinePositions().All(m => board.GetCell(m).IsRevealed), "mines revealed");
            Check("失败表情为晕眩", FaceText.Text == "😵", FaceText.Text);
            Check("失败状态文案正确", StatusText.Text.StartsWith(LocPrefix("status.exploded"), StringComparison.Ordinal), StatusText.Text);
            Check("失败后棋盘拒绝继续操作", board.ToggleFlag(new Cell(0, 0)).Accepted == false, "ToggleFlag");

            // ── 8 · 难度切换与窗口自适应 ────────────────────────────────────────
            StartNewGame(BoardPreset.Intermediate, DifficultyLevel.Intermediate, 606);
            await Task.Delay(120);
            Check("中级棋盘 16x16", Board.CellControlCount == 256, $"count={Board.CellControlCount}");
            Check("难度选择器切到中级", DifficultyBar.SelectedItem == ItemIntermediate, DifficultyBar.SelectedItem?.Text ?? "null");

            StartNewGame(BoardPreset.Expert, DifficultyLevel.Expert, 99);
            await Task.Delay(120);
            Check("高级棋盘 16x30", Board.CellControlCount == 480, $"count={Board.CellControlCount}");
            Check("高级计数板显示 099", MineCounterText.Text == "099", MineCounterText.Text);
            double cellSize = Board.CellSize;
            Check("格子尺寸落在 18–46 之间", cellSize >= 18 && cellSize <= 46, $"cell={cellSize:0.##}");

            double scale = RootGrid.XamlRoot!.RasterizationScale;
            var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var size = AppWindow.Size;
            Check("窗口不超出工作区", size.Width <= work.Width && size.Height <= work.Height,
                $"window={size.Width}x{size.Height} work={work.Width}x{work.Height} scale={scale}");

            // ── 9 · 自定义棋盘校验 ──────────────────────────────────────────────
            Check("合法自定义盘通过校验", Minefield.IsValid(16, 16, 40, out _), "16x16/40");
            Check("雷数过多被拒绝", !Minefield.IsValid(9, 9, 81, out _), "9x9/81");
            Check("行数越界被拒绝", !Minefield.IsValid(31, 10, 10, out _), "31x10/10");
            Check("列数越界被拒绝", !Minefield.IsValid(10, 41, 10, out _), "10x41/10");
            Check("边界值 30x40 可用", Minefield.IsValid(30, 40, 200, out _), "30x40/200");

            StartNewGame(new BoardPreset(12, 20, 45), DifficultyLevel.Custom, 5);
            await Task.Delay(80);
            Check("自定义棋盘生效", Board.CellControlCount == 240, $"count={Board.CellControlCount}");
            Check("自定义棋盘不计最佳成绩", BestTimeText.Text == Loc.T("footer.customNoBest"), BestTimeText.Text);

            // ── 10 · 设置持久化 ────────────────────────────────────────────────
            var settings = new GameSettings { Theme = AppTheme.Dark, LastDifficulty = DifficultyLevel.Expert };
            settings.CustomBoard = new BoardPreset(12, 20, 45);
            var restored = GameSettings.FromJson(settings.ToJson());
            Check("设置 JSON 往返保持主题", restored.Theme == AppTheme.Dark, restored.Theme.ToString());
            Check("设置 JSON 往返保持难度", restored.LastDifficulty == DifficultyLevel.Expert, restored.LastDifficulty.ToString());
            Check("设置 JSON 往返保持自定义盘", restored.CustomBoard == new BoardPreset(12, 20, 45), restored.CustomBoard.ToString());
            Check("损坏的 JSON 回退默认设置", GameSettings.FromJson("{oops").Theme == AppTheme.System, "fallback");

            // ── 11 · 主题切换 ──────────────────────────────────────────────────
            SetCaptureTheme(AppTheme.Dark);
            await Task.Delay(120);
            Check("深色主题生效", RootGrid.ActualTheme == ElementTheme.Dark, RootGrid.ActualTheme.ToString());
            SetCaptureTheme(AppTheme.Light);
            await Task.Delay(120);
            Check("浅色主题生效", RootGrid.ActualTheme == ElementTheme.Light, RootGrid.ActualTheme.ToString());

            // ── 12 · 快捷键（不挂 KeyboardAccelerator，避免棋盘上冒出 “F2” 提示气泡）──
            Check("棋盘上没有加速键气泡来源", Board.KeyboardAccelerators.Count == 0 && RootGrid.KeyboardAccelerators.Count == 0,
                $"board={Board.KeyboardAccelerators.Count} root={RootGrid.KeyboardAccelerators.Count}");
            Check("棋盘不设置工具提示", ToolTipService.GetToolTip(Board) is null, "ToolTip");

            // The board used to be the trigger for the "F2" keytip bubble. Anything the
            // NavigationView shell brings along is fine, as long as it is not the board itself.
            var acceleratorHosts = new List<string>();
            int boardHosts = CountAcceleratorHosts(Board);
            CountAcceleratorHosts(RootGrid, acceleratorHosts);
            Check("棋盘内没有加速键提示源", boardHosts == 0, $"board={boardHosts}");
            Check("加速键提示源都在棋盘之外", acceleratorHosts.All(host => !host.StartsWith("BoardView", StringComparison.Ordinal)),
                string.Join(",", acceleratorHosts));

            StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 20240607);
            await Task.Delay(80);
            Board.RevealAt(new Cell(4, 4));
            await Task.Delay(80);
            int beforeReset = _board!.RevealedCount;
            Check("F2 重开生效", ApplyShortcut(VirtualKey.F2, false) && _board!.RevealedCount == 0 && beforeReset > 0,
                $"before={beforeReset} after={_board!.RevealedCount}");
            Check("Ctrl+N 重开生效", ApplyShortcut(VirtualKey.N, true), "Ctrl+N");
            Check("Ctrl+3 切到高级", ApplyShortcut(VirtualKey.Number3, true)
                && Board.CellControlCount == 480 && DifficultyBar.SelectedItem == ItemExpert, "Ctrl+3");
            Check("Ctrl+1 切回初级", ApplyShortcut(VirtualKey.Number1, true)
                && Board.CellControlCount == 81 && DifficultyBar.SelectedItem == ItemBeginner, "Ctrl+1");
            Check("无修饰键的数字键不切难度", !ApplyShortcut(VirtualKey.Number2, false), "Number2 alone");
            Check("其它按键不被吞掉", !ApplyShortcut(VirtualKey.A, false) && !ApplyShortcut(VirtualKey.A, true), "A");

            // ── 13 · 自定义棋盘对话框不显示那对又大又丑的上下箭头 ──────────────
            var dialog = new Views.CustomGameDialog(new BoardPreset(16, 16, 40));
            var boxes = new[] { "ColumnsBox", "RowsBox", "MinesBox" }
                .Select(name => dialog.FindName(name) as NumberBox)
                .ToArray();
            Check("自定义棋盘对话框的三个输入框都存在", boxes.All(box => box is not null),
                string.Join(",", boxes.Select(box => box?.Name ?? "null")));
            Check("所有数字输入框都隐藏了上下箭头",
                boxes.All(box => box is not null && box.SpinButtonPlacementMode == NumberBoxSpinButtonPlacementMode.Hidden),
                string.Join(",", boxes.Select(box => box?.SpinButtonPlacementMode.ToString() ?? "null")));
            Check("数字输入框仍限定在合法范围",
                boxes[0]!.Minimum == 3 && boxes[0]!.Maximum == 40
                && boxes[1]!.Minimum == 3 && boxes[1]!.Maximum == 30,
                $"cols={boxes[0]!.Minimum}-{boxes[0]!.Maximum} rows={boxes[1]!.Minimum}-{boxes[1]!.Maximum}");

            // ── 14 · 音效（自绘 WAV + WinUI 自带控件音效）─────────────────────
            var click = GameSounds.RenderWav(GameSound.Click);
            var reveal = GameSounds.RenderWav(GameSound.Reveal);
            var win = GameSounds.RenderWav(GameSound.Win);
            string header = System.Text.Encoding.ASCII.GetString(click, 0, 4) + System.Text.Encoding.ASCII.GetString(click, 8, 4);
            Check("音效是合法的 PCM WAV", header == "RIFFWAVE"
                && BitConverter.ToInt16(click, 20) == 1
                && BitConverter.ToInt16(click, 22) == 1
                && BitConverter.ToInt32(click, 24) == 44100
                && BitConverter.ToInt32(click, 40) == click.Length - 44,
                $"{header} rate={BitConverter.ToInt32(click, 24)}");

            double RevealSeconds(byte[] wav) => (wav.Length - 44) / 2.0 / 44100.0;
            Check("音效时长符合设计", Math.Abs(RevealSeconds(click) - 0.022) < 0.002
                && Math.Abs(RevealSeconds(reveal) - 0.055) < 0.004
                && Math.Abs(RevealSeconds(win) - 0.62) < 0.02,
                $"click={RevealSeconds(click):0.000}s reveal={RevealSeconds(reveal):0.000}s win={RevealSeconds(win):0.000}s");

            // Zero-crossing estimate of the dominant tone: the reveal blip is an 880 Hz sine.
            int crossings = 0;
            for (int i = 0; i < (reveal.Length - 44) / 2 - 1; i++)
            {
                short a = BitConverter.ToInt16(reveal, 44 + i * 2);
                short b = BitConverter.ToInt16(reveal, 44 + (i + 1) * 2);
                if ((a < 0 && b >= 0) || (a >= 0 && b < 0))
                {
                    crossings++;
                }
            }

            double estimatedHz = crossings / 2.0 / RevealSeconds(reveal);
            Check("翻开音的音高正确", Math.Abs(estimatedHz - 880) < 60, $"{estimatedHz:0} Hz");

            bool wasEnabled = GameSounds.Enabled;
            GameSounds.Enabled = false;
            Check("静音后不再输出", !GameSounds.Play(GameSound.Reveal), "muted");
            GameSounds.Enabled = true;

            bool played = GameSounds.Play(GameSound.Reveal);
            Check("音效播放被音频设备接受", played, played ? "PlaySound=true" : "PlaySound=false（无音频端点）");
            GameSounds.Enabled = wasEnabled;

            UiSounds.Play(ElementSoundKind.Invoke);
            Check("WinUI 控件音效可用", UiSounds.Available, UiSounds.HasPlayed ? "played" : "not played");
            UiSounds.Enabled = false;
            UiSounds.Play(ElementSoundKind.Invoke);
            UiSounds.Enabled = true;
            Check("静音时控件音效安全跳过", UiSounds.Available, "ok");

            // ── 15 · 多语言（简体中文 / English / 日本語 / Français / 한국어）────
            var languages = LanguageResolver.Supported.ToArray();
            var referenceKeys = Loc.Table(AppLanguage.English).Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            bool sameKeySets = languages.All(language =>
            {
                var keys = Loc.Table(language).Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
                return keys.Length == referenceKeys.Length && keys.SequenceEqual(referenceKeys);
            });
            Check("五种语言的字符串表键集完全一致", sameKeySets,
                $"keys={referenceKeys.Length} languages={languages.Length}");

            var emptyValues = languages
                .Where(language => Loc.Table(language).Values.Any(string.IsNullOrWhiteSpace))
                .Select(LanguageResolver.Tag)
                .ToArray();
            Check("字符串表没有空值", emptyValues.Length == 0, string.Join(",", emptyValues));

            var placeholderIssues = new List<string>();
            foreach (string key in referenceKeys)
            {
                string[] expected = Placeholders(Loc.Table(AppLanguage.English)[key]);
                if (expected.Length == 0)
                {
                    continue;
                }

                foreach (var language in languages)
                {
                    if (!expected.SequenceEqual(Placeholders(Loc.Table(language)[key])))
                    {
                        placeholderIssues.Add($"{LanguageResolver.Tag(language)}:{key}");
                    }
                }
            }

            Check("每种语言的格式占位符保持一致", placeholderIssues.Count == 0,
                string.Join(",", placeholderIssues.Take(4)));

            Check("系统语言映射：zh / zh-TW → 简体中文",
                LanguageResolver.FromCulture(new CultureInfo("zh-CN")) == AppLanguage.ChineseSimplified
                && LanguageResolver.FromCulture(new CultureInfo("zh-TW")) == AppLanguage.ChineseSimplified
                && LanguageResolver.FromCulture(new CultureInfo("zh-Hans-CN")) == AppLanguage.ChineseSimplified,
                $"{LanguageResolver.FromCulture(new CultureInfo("zh-TW"))}");
            Check("系统语言映射：ja / fr-CA / ko / en-GB",
                LanguageResolver.FromCulture(new CultureInfo("ja-JP")) == AppLanguage.Japanese
                && LanguageResolver.FromCulture(new CultureInfo("fr-CA")) == AppLanguage.French
                && LanguageResolver.FromCulture(new CultureInfo("ko-KR")) == AppLanguage.Korean
                && LanguageResolver.FromCulture(new CultureInfo("en-GB")) == AppLanguage.English,
                "ja/fr/ko/en");
            Check("没有对应语言表时回退到英语",
                LanguageResolver.FromCulture(new CultureInfo("de-DE")) == AppLanguage.English
                && LanguageResolver.FromCulture(CultureInfo.InvariantCulture) == AppLanguage.English,
                $"{LanguageResolver.FromCulture(new CultureInfo("de-DE"))}");
            Check("跟随系统时解析成具体语言",
                LanguageResolver.Resolve(AppLanguage.System, new CultureInfo("ja-JP")) == AppLanguage.Japanese,
                "System + ja-JP");
            Check("手动选择优先于系统语言",
                LanguageResolver.Resolve(AppLanguage.French, new CultureInfo("ja-JP")) == AppLanguage.French,
                "French + ja-JP");

            var seenLabels = new HashSet<string>(StringComparer.Ordinal);
            bool appliedEverywhere = true;
            foreach (var language in languages)
            {
                CaptureLanguage(language);
                await Task.Delay(40);
                appliedEverywhere &= TitleText.Text == Loc.T("app.title")
                    && Title == Loc.T("app.title")
                    && ItemBeginner.Text == Loc.T("difficulty.beginner")
                    && ItemCustom.Text == Loc.T("difficulty.custom")
                    && AutomationProperties.GetName(Board) == Loc.T("board.name");
                seenLabels.Add(ItemBeginner.Text);
            }

            Check("切换语言后标题、难度项、棋盘名称立即更新", appliedEverywhere,
                string.Join(" / ", seenLabels));
            Check("五种语言的难度名称互不相同", seenLabels.Count == languages.Length,
                string.Join(" / ", seenLabels.Take(3)));

            CaptureLanguage(AppLanguage.Japanese);
            StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 424242);
            await Task.Delay(60);
            Check("页脚使用当前语言", BestTimeText.Text.StartsWith(Loc.T("difficulty.beginner"), StringComparison.Ordinal),
                BestTimeText.Text);

            Board.RevealAt(new Cell(4, 4));
            await Task.Delay(60);
            var losingMine = FirstMine();
            if (losingMine is { } boom)
            {
                Board.RevealAt(boom);
            }

            await Task.Delay(80);
            Check("失败提示使用当前语言",
                StatusText.Text == Loc.F("status.exploded", ElapsedSeconds()),
                StatusText.Text);

            // Localized dialogs re-read the table on construction.
            var localizedDialog = new Views.CustomGameDialog(new BoardPreset(16, 16, 40));
            Check("自定义棋盘对话框标题/按钮本地化",
                localizedDialog.Title as string == Loc.T("custom.title")
                && localizedDialog.PrimaryButtonText == Loc.T("custom.primary")
                && localizedDialog.CloseButtonText == Loc.T("custom.cancel"),
                $"{localizedDialog.Title} / {localizedDialog.PrimaryButtonText}");

            // The NumberBoxes clamp whatever the user types, so an invalid state is hard to reach
            // through the UI; verify the localized wording of a rejected board directly instead.
            string localizedMineError = Loc.DescribeBoardError(BoardValidationError.MinesOutOfRange, 16, 16);
            string localizedRowError = Loc.DescribeBoardError(BoardValidationError.RowsOutOfRange, 16, 16);
            Check("非法参数的提示已本地化",
                localizedMineError == Loc.F("error.mines", (16 * 16) - 1)
                && localizedRowError == Loc.F("error.rows", Minefield.MinRows, Minefield.MaxRows)
                && localizedMineError != Loc.Table(AppLanguage.English)["error.mines"].Replace("{0}", ((16 * 16) - 1).ToString()),
                localizedMineError);

            if (localizedDialog.FindName("HintText") is TextBlock hint
                && localizedDialog.FindName("ColumnsLabel") is TextBlock columnsLabel)
            {
                Check("对话框标签与提示已本地化",
                    hint.Text == Loc.T("custom.hint") && columnsLabel.Text == Loc.T("custom.columns"),
                    hint.Text[..Math.Min(18, hint.Text.Length)]);
            }

            var settingsRoundTrip = GameSettings.FromJson(new GameSettings { Language = AppLanguage.Korean }.ToJson());
            Check("语言偏好能持久化", settingsRoundTrip.Language == AppLanguage.Korean,
                settingsRoundTrip.Language.ToString());
            Check("语言以可读形式写入 JSON", new GameSettings { Language = AppLanguage.French }.ToJson().Contains("\"French\"", StringComparison.Ordinal),
                "French");

            CaptureLanguage(AppLanguage.ChineseSimplified);
            StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 20240607);

            // ── 16 · 设置页（NavigationView + SettingsCard）──
            Check("导航栏有两个页面", Nav.MenuItems.Count == 2,
                string.Join(",", Nav.MenuItems.OfType<NavigationViewItem>().Select(i => i.Tag)));

            SelectPage(true);
            await Task.Delay(80);
            Check("切到设置页后游戏页隐藏",
                SettingsView.Visibility == Visibility.Visible && GameView.Visibility == Visibility.Collapsed,
                $"{SettingsView.Visibility}/{GameView.Visibility}");

            ShowPage(true);
            SettingsView.SyncFrom(new GameSettings { Theme = AppTheme.Dark, Language = AppLanguage.French, SoundEnabled = false });
            Check("设置页控件反映已保存的设置",
                SettingsView.SelectedTheme == AppTheme.Dark
                && SettingsView.SelectedLanguage == AppLanguage.French
                && !SettingsView.SoundOn,
                $"{SettingsView.SelectedTheme}/{SettingsView.SelectedLanguage}/{SettingsView.SoundOn}");

            SettingsView.SyncFrom(new GameSettings { Theme = AppTheme.System, Language = AppLanguage.System, SoundEnabled = true });
            Check("设置页默认项为跟随系统",
                SettingsView.SelectedTheme == AppTheme.System && SettingsView.SelectedLanguage == AppLanguage.System && SettingsView.SoundOn,
                $"{SettingsView.SelectedTheme}/{SettingsView.SelectedLanguage}/{SettingsView.SoundOn}");

            // The page reports changes; the window is what persists them.
            SettingsView_ThemeChanged(SettingsView, AppTheme.Dark);
            await Task.Delay(80);
            Check("设置页改主题即时生效",
                _settings.Theme == AppTheme.Dark && RootGrid.ActualTheme == ElementTheme.Dark,
                $"{_settings.Theme}/{RootGrid.ActualTheme}");
            SettingsView_ThemeChanged(SettingsView, AppTheme.Light);
            await Task.Delay(80);

            SettingsView_SoundChanged(SettingsView, false);
            Check("设置页关音效同步到两层",
                !_settings.SoundEnabled && !GameSounds.Enabled && !UiSounds.Enabled,
                $"{_settings.SoundEnabled}/{GameSounds.Enabled}/{UiSounds.Enabled}");
            SettingsView_SoundChanged(SettingsView, true);
            Check("设置页开音效恢复", _settings.SoundEnabled && GameSounds.Enabled && UiSounds.Enabled, "on");

            CaptureLanguage(AppLanguage.Japanese);
            SettingsView.ApplyLocalization();
            await Task.Delay(60);
            Check("设置页文案跟随语言",
                SettingsView.ThemeHeader == Loc.T("settings.theme") && SettingsView.LanguageOptionCount == LanguageResolver.Supported.Count + 1,
                $"{SettingsView.ThemeHeader}/{SettingsView.LanguageOptionCount}");

            // The ComboBox snapshots the selected item's text, so the closed box used to keep the old
            // language after a switch. The dropped selection must also survive the refresh.
            // The ComboBox snapshots the selected item's text, so the closed box used to keep the old
            // language after a switch.
            CaptureLanguage(AppLanguage.Japanese);
            SettingsView.SyncFrom(_settings);
            SettingsView.ApplyLocalization();
            await Task.Delay(60);
            string expectedThemeLabel = _settings.Theme switch
            {
                AppTheme.Light => Loc.T("menu.themeLight"),
                AppTheme.Dark => Loc.T("menu.themeDark"),
                _ => Loc.T("menu.themeSystem"),
            };
            Check("语言切换后下拉框显示文本也跟随语言",
                SettingsView.SelectionTextsAreCurrent
                && SettingsView.ThemeSelectionText == expectedThemeLabel
                && SettingsView.LanguageSelectionText == LanguageResolver.NativeName(_settings.Language),
                $"theme=[{SettingsView.ThemeSelectionText}] want=[{expectedThemeLabel}] lang=[{SettingsView.LanguageSelectionText}] current={SettingsView.SelectionTextsAreCurrent}");

            // Picking a language from the dropdown used to tear down the ComboBox item collection
            // from inside its own SelectionChanged handler, which crashed the process.
            Check("下拉里有全部语言选项", SettingsView.SelectLanguageForTest(AppLanguage.Korean), "korean found");
            await Task.Delay(200);
            Check("在下拉里选语言不崩溃且立即生效",
                _settings.Language == AppLanguage.Korean && Loc.Current == AppLanguage.Korean
                && SettingsView.SelectedLanguage == AppLanguage.Korean,
                $"{_settings.Language}/{Loc.Current}/{SettingsView.SelectedLanguage}");
            Check("选完语言后选项没有被重建",
                SettingsView.LanguageOptionCount == LanguageResolver.Supported.Count + 1,
                SettingsView.LanguageOptionCount.ToString());

            // Same pick, but with the popup actually open: this is where the item containers exist
            // and where tearing the collection down would have crashed.
            SettingsView.SetLanguageDropDownForTest(true);
            await Task.Delay(120);
            bool pickedInPopup = SettingsView.SelectLanguageForTest(AppLanguage.Japanese);
            SettingsView.SetLanguageDropDownForTest(false);
            await Task.Delay(250);
            Check("展开下拉时选语言也不崩溃",
                pickedInPopup && _settings.Language == AppLanguage.Japanese && Loc.Current == AppLanguage.Japanese,
                $"{_settings.Language}/{Loc.Current}");
            Check("下拉关闭后显示文本已刷新",
                SettingsView.SelectionTextsAreCurrent
                && SettingsView.LanguageSelectionText == LanguageResolver.NativeName(AppLanguage.Japanese),
                $"[{SettingsView.LanguageSelectionText}] current={SettingsView.SelectionTextsAreCurrent}");

            SelectPage(false);
            await Task.Delay(60);
            Check("返回后游戏页恢复", GameView.Visibility == Visibility.Visible && SettingsView.Visibility == Visibility.Collapsed,
                $"{GameView.Visibility}/{SettingsView.Visibility}");
            Check("返回后选中项回到游戏", ReferenceEquals(Nav.SelectedItem, NavGameItem), Nav.SelectedItem?.ToString() ?? "null");

            CaptureLanguage(AppLanguage.ChineseSimplified);
            ApplyThemeSetting();
            StartNewGame(BoardPreset.Beginner, DifficultyLevel.Beginner, 20240607);
        }
        catch (Exception ex)
        {
            failures++;
            total++;
            lines.Add($"FAIL  自检过程抛出异常   [{ex.GetType().Name}: {ex.Message}]");
            App.LogCrash(ex);
        }

        lines.Insert(0, $"WinMine 自检报告 · {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        lines.Insert(1, $"应用版本 {AppVersion} · 引擎 {typeof(Minefield).Assembly.GetName().Version} · WinUI {Environment.Version}");
        lines.Add(string.Empty);
        lines.Add($"RESULT: {(failures == 0 ? "ALL PASS" : failures + " FAILED")}  ({total - failures}/{total})");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            await File.WriteAllLinesAsync(reportPath, lines);

            // Drop the synthesised effects next to the report so they can be auditioned.
            string audioDir = Path.Combine(Path.GetDirectoryName(reportPath)!, "audio");
            Directory.CreateDirectory(audioDir);
            foreach (GameSound sound in Enum.GetValues<GameSound>())
            {
                await File.WriteAllBytesAsync(
                    Path.Combine(audioDir, $"{sound}.wav"),
                    GameSounds.RenderWav(sound));
            }
        }
        catch (IOException)
        {
        }

        await Task.Delay(150);
        Application.Current.Exit();
    }

    /// <summary>Distinct <c>{0}</c>-style format items of a template, sorted so tables can be compared.</summary>
    private static string[] Placeholders(string template) =>
        System.Text.RegularExpressions.Regex.Matches(template, @"\{\d+\}")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Template text before its first placeholder — lets checks stay language-neutral.</summary>
    private static string LocPrefix(string key)
    {
        string template = Loc.T(key);
        int placeholder = template.IndexOf('{', StringComparison.Ordinal);
        return placeholder < 0 ? template : template[..placeholder];
    }

    private static int CountAcceleratorHosts(DependencyObject node, List<string>? found = null)
    {
        bool isHost = node is UIElement element && element.KeyboardAccelerators.Count > 0;
        if (isHost)
        {
            string name = node is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name) ? $"#{fe.Name}" : string.Empty;
            string keys = node is UIElement host
                ? string.Join("+", host.KeyboardAccelerators.Select(a => $"{a.Modifiers}:{a.Key}"))
                : string.Empty;
            found?.Add($"{node.GetType().Name}{name}({keys})");
        }

        int count = isHost ? 1 : 0;
        int children = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < children; i++)
        {
            count += CountAcceleratorHosts(VisualTreeHelper.GetChild(node, i), found);
        }

        return count;
    }

    private Cell? FirstHiddenSafeCell()
    {
        if (_board is null)
        {
            return null;
        }

        foreach (var cell in _board.AllCells())
        {
            var info = _board.GetCell(cell);
            if (!info.IsRevealed && !info.IsFlagged && !_board.IsMineAt(cell))
            {
                return cell;
            }
        }

        return null;
    }

    private void RevealAllSafeCells()
    {
        if (_board is null)
        {
            return;
        }

        foreach (var cell in _board.AllCells().ToList())
        {
            if (_board.Status is GameStatus.Won or GameStatus.Lost)
            {
                return;
            }

            if (!_board.IsMineAt(cell) && !_board.GetCell(cell).IsRevealed)
            {
                Board.RevealAt(cell);
            }
        }
    }
}
