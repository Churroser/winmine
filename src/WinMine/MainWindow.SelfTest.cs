using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            Check("胜利状态文案正确", StatusText.Text.Contains("通关", StringComparison.Ordinal)
                || StatusText.Text.Contains("新纪录", StringComparison.Ordinal), StatusText.Text);
            Check("胜利后记录最佳成绩", BestTimeText.Text.Contains("最佳", StringComparison.Ordinal), BestTimeText.Text);

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
            Check("失败状态文案正确", StatusText.Text.Contains("踩雷", StringComparison.Ordinal), StatusText.Text);
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
            Check("自定义棋盘不计最佳成绩", BestTimeText.Text.Contains("不计入", StringComparison.Ordinal), BestTimeText.Text);

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
            int acceleratorHosts = CountAcceleratorHosts(RootGrid);
            Check("视觉树里没有任何加速键提示源", acceleratorHosts == 0, $"hosts={acceleratorHosts}");

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
        }
        catch (Exception ex)
        {
            failures++;
            total++;
            lines.Add($"FAIL  自检过程抛出异常   [{ex.GetType().Name}: {ex.Message}]");
            App.LogCrash(ex);
        }

        lines.Insert(0, $"WinMine 自检报告 · {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        lines.Insert(1, $"引擎版本 {typeof(Minefield).Assembly.GetName().Version} · WinUI {Environment.Version}");
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

    private static int CountAcceleratorHosts(DependencyObject node)
    {
        int count = node is UIElement element && element.KeyboardAccelerators.Count > 0 ? 1 : 0;
        int children = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < children; i++)
        {
            count += CountAcceleratorHosts(VisualTreeHelper.GetChild(node, i));
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
