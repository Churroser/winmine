# 扫雷 · WinMine

简体中文 · [English](README.en.md)

用 **WinUI 3 / Windows App SDK** 复刻的经典扫雷，按 Windows 11 Fluent Design 重做界面：Mica 背景、自定义标题栏、圆角卡片、Segoe Fluent Icons 图标、浅色/深色主题自适应。

![初级·浅色](docs/screenshots/01-beginner-new-light.png)

| 进行中（浅色） | 通关（浅色） | 踩雷（浅色） |
| --- | --- | --- |
| ![](docs/screenshots/02-beginner-playing-light.png) | ![](docs/screenshots/03-beginner-won-light.png) | ![](docs/screenshots/04-beginner-lost-light.png) |

| 中级（深色） | 高级（深色） | 自定义棋盘对话框 |
| --- | --- | --- |
| ![](docs/screenshots/05-intermediate-playing-dark.png) | ![](docs/screenshots/06-expert-playing-dark.png) | ![](docs/screenshots/08-custom-dialog-light.png) |

## 功能

| 分类 | 内容 |
| --- | --- |
| 玩法 | 左键翻开 · 右键插旗 · 中键或再次点击已翻开的数字快速展开（chord）· 第一次点击永远安全（3×3 保护区） |
| 键盘 | 方向键移动；空格/回车翻开或展开；`F` 插旗；`F2` / `Ctrl+N` 重开；`Ctrl+1/2/3` 切换初级/中级/高级（无焦点冲突，鼠标悬停棋盘不会弹出按键气泡） |
| 难度 | 初级 9×9/10 雷、中级 16×16/40 雷、高级 16×30/99 雷、自定义（列 3–40，行 3–30；输入框无上下箭头，直接键入数字，取值自动夹紧） |
| 状态栏 | 剩余雷数、计时器（0–999 秒）、表情按钮（点击重开）、最佳成绩 |
| 界面 | 自动跟随系统深浅色或手动指定；窗口按棋盘大小自适应并居中；DWM 标题栏按钮配色跟随主题 |
| 音效 | 翻开 / 泛洪 / 插旗 / 取消插旗 / 快速展开 / 爆炸 / 通关，全部为运行时合成的 PCM 音块；按钮与菜单用 WinUI 自带的 `ElementSoundPlayer`；可在设置里静音 |
| 设置 | 外观（跟随系统 / 浅色 / 深色）、音效开关、重置最佳成绩、关于 |

## 构建与运行

需要 .NET 10 SDK 与 Windows App Runtime 2.6（`Microsoft.WindowsAppSDK 2.6.4`，非自包含）。

```powershell
dotnet build WinMine.sln -c Release
dotnet run --project src/WinMine/WinMine.csproj
```

可执行文件（非打包 Win32 应用，可直接双击）：

```
src/WinMine/bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/WinMine.exe
```

发布单目录版本：

```powershell
dotnet publish src/WinMine/WinMine.csproj -c Release -r win-x64 --self-contained false -o publish
```

## 项目结构

```
WinMine.sln
src/WinMine.Core/            纯 .NET 游戏引擎（无 UI 依赖，可单元测试）
  Cell.cs                    Cell / CellInfo / MoveResult / GameStatus
  Minefield.cs               棋盘状态机：延迟布雷、泛洪、插旗、chord、胜负判定
  Difficulty.cs              BoardPreset 与 DifficultyPresets
  BestTimes.cs               最佳成绩
  GameSettings.cs            JSON 设置模型
src/WinMine/                 WinUI 3 应用
  MainWindow.xaml(.cs)       Fluent 布局、标题栏、计时、主题、菜单
  MainWindow.Capture.cs      --capture DIR 截图自检
  MainWindow.SelfTest.cs     --selftest FILE 交互自检
  Controls/BoardView.cs      自绘棋盘面板（输入、动画、焦点环）
  Controls/CellVisual.cs     单格视觉（圆角瓷砖 + 矢量旗/雷/错旗）
  Controls/BoardPalette.cs   双主题调色板
  Views/CustomGameDialog.xaml 自定义棋盘对话框
  Services/SettingsStore.cs  %LOCALAPPDATA%\WinMine\settings.json
  Services/GameSounds.cs     运行时合成的 PCM 音效 + winmm PlaySound
  Services/UiSounds.cs       WinUI 自带 ElementSoundPlayer 包装（Invoke/MoveNext…）
tests/WinMine.Core.Tests/    28 个 xunit 测试
tools/make-icon.ps1          生成 Assets/winmine.png + winmine.ico
tools/set-repo-meta.ps1      通过 REST API 设置仓库 description / topics
tools/repo-description.txt   给上面那个脚本读的 UTF-8 描述文案
```

## 验证

```powershell
# 引擎单元测试（28 项）
dotnet test tests/WinMine.Core.Tests/WinMine.Core.Tests.csproj

# 界面自检：驱动真实窗口与棋盘控件，输出 PASS/FAIL 报告
src/WinMine/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/WinMine.exe --selftest artifacts/selftest.txt

# 界面截图：把脚本化的 8 个状态渲染成 PNG（不需要可见桌面）
src/WinMine/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/WinMine.exe --capture artifacts/shots
```

`--selftest` 覆盖 68 项检查：首点安全、泛洪范围、插旗/取消插旗/已翻开格拒绝插旗、chord 展开、计时器走动、通关自动插旗与最佳成绩、踩雷揭示全部地雷与拒绝后续操作、三档难度与自定义棋盘的格子数量、窗口不超出工作区、设置 JSON 往返、主题切换，快捷键与“界面里不存在加速键提示源”（专门守住“悬停棋盘冒出 F2 气泡”这个回归），自定义棋盘对话框的输入框状态（全部隐藏上下箭头、取值区间受控），以及音效（WAV 头合法、时长、880 Hz 音高实测、静音后不输出、音频设备接受播放、WinUI 控件音效可用）。

自检还会把 8 个合成音效写到报告旁的 `audio/*.wav`，方便直接试听——仓库里也放了一份在 [docs/sounds](docs/sounds)。

> 开发机若处于锁屏（Remote Desktop 断开等）会话，`PrintWindow` / `CopyFromScreen` 拿不到窗口像素，因此自检改用应用内 `RenderTargetBitmap` 渲染真实视觉树；Mica 属于合成器背景、不在 XAML 内容里，截图时用近似纯色背景替代。

## 设计说明

- **引擎与界面分离**：`WinMine.Core` 是纯状态机，`BoardView` 只负责把 `CellInfo` 画出来并把鼠标/键盘翻译成 `Reveal`/`ToggleFlag`/`Chord`，因此规则可以脱离 UI 测试。
- **自绘棋盘**：不用 `GridView`/`Button`，而是继承 `Panel` 手工布置 `CellVisual`（9×9 到 30×40 共 1200 格）——没有控件模板开销，且能精确控制瓷砖圆角、间距、悬停/按下缩放与逐格揭晓动画。
- **防作弊**：`Minefield.GetCell` 只在失败或该格已揭示时返回 `IsMine`，界面无法通过数据提前得知雷的位置；`BoardView` 另用 `IsMineAt`（内部真值）决定揭示态下的雷图标。
- **视觉细节**：瓷砖圆角 4、间隙 3（间隙露出棋盘底色，形成网格线）、数字使用 `Segoe UI Variable Display` 半粗体并按经典配色（1 蓝 2 绿 3 红 4 紫 5 棕 6 青 7 黑 8 灰），深色主题重新映射为高对比色。
- **窗口自适应**：换难度时从 46px 到 18px 逐档试探格子尺寸，取能塞进工作区的最大值后重设窗口客户区并居中。
- **快捷键不走 `KeyboardAccelerator`**：`KeyboardAccelerator` 挂在根元素上时，WinUI 会在指针悬停该元素（包括整个棋盘）时弹出按键气泡（显示 “F2”）。所以窗口级快捷键改为在根 `Grid` 上挂 `KeyDown`（`handledEventsToo`），自行判断 `F2` / `Ctrl+N` / `Ctrl+1..3`；模态对话框打开期间不响应。
- **音效分两层**：WinUI 3 确实自带控件音效库 `Microsoft.UI.Xaml.Controls.ElementSoundPlayer` + `Microsoft.UI.Xaml.ElementSoundKind`（Fluent 的 Invoke / Focus / Show / Hide / MoveNext … 语义音，没有“爆炸”“胜利”这种玩法音，且只能选预设音色）。所以按钮、菜单、难度切换交给 `ElementSoundPlayer`，玩法音效则由 `Services/GameSounds.cs` 在启动时合成 PCM 并交给 `winmm!PlaySound(SND_MEMORY | SND_ASYNC)` 播放——零音频资源文件、零 NuGet 依赖，点击延迟只有几十微秒，音块用 `GCHandle` 永久钉住以满足异步读取的生命周期。
