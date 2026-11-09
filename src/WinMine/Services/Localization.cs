using System.Globalization;
using WinMine.Core;

namespace WinMine.Services;

/// <summary>
/// Tiny string-table based localization. Five tables are compiled in (Simplified Chinese,
/// English, Japanese, French, Korean) because the app has no package identity, so there is no
/// PRI/ResourceManager pipeline to lean on; a plain dictionary lookup keeps things debuggable
/// and lets <c>--selftest</c> assert that every table carries exactly the same keys.
///
/// Lookup falls back to English for a missing key, which the self-test makes sure never happens.
/// </summary>
public static class Loc
{
    private static AppLanguage _current = LanguageResolver.DefaultLanguage;

    /// <summary>Language currently being rendered.</summary>
    public static AppLanguage Current => _current;

    /// <summary>Raised whenever <see cref="SetLanguage"/> actually changes the language.</summary>
    public static event Action<AppLanguage>? LanguageChanged;

    public static void SetLanguage(AppLanguage language)
    {
        if (language == AppLanguage.System)
        {
            language = LanguageResolver.Resolve(AppLanguage.System);
        }

        if (language == _current)
        {
            return;
        }

        _current = language;
        LanguageChanged?.Invoke(language);
    }

    /// <summary>Every key of the reference (English) table, in table order.</summary>
    public static IReadOnlyList<string> Keys => English.Keys.ToArray();

    public static IReadOnlyDictionary<string, string> Table(AppLanguage language) => language switch
    {
        AppLanguage.ChineseSimplified => ChineseSimplified,
        AppLanguage.Japanese => Japanese,
        AppLanguage.French => French,
        AppLanguage.Korean => Korean,
        _ => English,
    };

    public static string T(string key)
    {
        if (Table(_current).TryGetValue(key, out string? value))
        {
            return value;
        }

        return English.TryGetValue(key, out string? fallback) ? fallback : key;
    }

    /// <summary>Looks a key up and runs it through <see cref="string.Format(string, object?[])"/>.</summary>
    public static string F(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    public static string DifficultyName(DifficultyLevel level) => T(level switch
    {
        DifficultyLevel.Intermediate => "difficulty.intermediate",
        DifficultyLevel.Expert => "difficulty.expert",
        DifficultyLevel.Custom => "difficulty.custom",
        _ => "difficulty.beginner",
    });

    /// <summary>Message for a rejected custom board, in the current UI language.</summary>
    public static string DescribeBoardError(BoardValidationError error, int rows, int columns) => error switch
    {
        BoardValidationError.RowsOutOfRange => F("error.rows", Minefield.MinRows, Minefield.MaxRows),
        BoardValidationError.ColumnsOutOfRange => F("error.columns", Minefield.MinColumns, Minefield.MaxColumns),
        BoardValidationError.MinesOutOfRange => F("error.mines", Math.Max(1, (rows * columns) - 1)),
        _ => T("custom.invalid"),
    };

    private static Dictionary<string, string> Table(params string[] keyValues)
    {
        var table = new Dictionary<string, string>(keyValues.Length / 2, StringComparer.Ordinal);
        for (int i = 0; i + 1 < keyValues.Length; i += 2)
        {
            table[keyValues[i]] = keyValues[i + 1];
        }

        return table;
    }

    private static readonly Dictionary<string, string> English = Table(
        "app.title", "Fluent Sweeper",
        "board.name", "Fluent Sweeper board",
        "button.newGame", "New game (F2)",
        "button.settings", "Settings",
        "button.restart", "Restart (F2)",
        "difficulty.beginner", "Beginner",
        "difficulty.intermediate", "Intermediate",
        "difficulty.expert", "Expert",
        "difficulty.custom", "Custom",
        "footer.best", "{0} · best {1} s",
        "footer.noBest", "{0} · no best time yet",
        "footer.customNoBest", "Custom board · not ranked",
        "status.exploded", "Hit a mine — {0} s",
        "status.newRecord", "New record! {0} s",
        "status.won", "Cleared in {0} s",
        "status.bestCleared", "Best times cleared",
        "menu.appearance", "Appearance",
        "menu.themeSystem", "Use system setting",
        "menu.themeLight", "Light",
        "menu.themeDark", "Dark",
        "menu.sound", "Sound effects",
        "menu.language", "Language",
        "menu.languageSystem", "Use system language",
        "menu.resetBestTimes", "Reset best times",
        "menu.about", "About Fluent Sweeper",
        "dialog.reset.title", "Reset best times",
        "dialog.reset.body", "Every difficulty's best time will be cleared. This cannot be undone.",
        "dialog.reset.primary", "Reset",
        "action.cancel", "Cancel",
        "dialog.about.tagline", "Classic Minesweeper · WinUI 3 / Fluent Design rebuild",
        "dialog.about.controls", "Left-click reveals · right-click flags · middle-click or re-click a number to chord · arrows + Space",
        "dialog.about.version", "Version {0}",
        "action.close", "Close",
        "custom.title", "Custom board",
        "custom.primary", "Start game",
        "custom.cancel", "Cancel",
        "custom.columns", "Columns (width)",
        "custom.rows", "Rows (height)",
        "custom.mines", "Mines",
        "custom.invalid", "These values do not work",
        "custom.hint", "Columns 3–40, rows 3–30, mines at most cells − 1. The first click never hits a mine.",
        "error.rows", "Rows must be between {0} and {1}.",
        "error.columns", "Columns must be between {0} and {1}.",
        "error.mines", "Mines must be between 1 and {0}.",
        "nav.game", "Game",
        "nav.settings", "Settings",
        "settings.title", "Settings",
        "settings.appearance", "Appearance",
        "settings.sound", "Sound",
        "settings.language", "Language",
        "settings.data", "Data",
        "settings.aboutTitle", "About",
        "settings.theme", "Theme",
        "settings.themeDesc", "Follow Windows, or pick light or dark",
        "settings.soundDesc", "Reveals, flags, explosions and button clicks",
        "settings.languageDesc", "Follows the Windows display language",
        "settings.resetTitle", "Best times",
        "settings.resetDesc", "Clears the best time of every difficulty",
        "settings.resetButton", "Clear",
        "settings.version", "Version {0}",
        "settings.viewSource", "View on GitHub");

    private static readonly Dictionary<string, string> ChineseSimplified = Table(
        "app.title", "Fluent Sweeper",
        "board.name", "Fluent Sweeper 棋盘",
        "button.newGame", "新游戏 (F2)",
        "button.settings", "设置",
        "button.restart", "重新开始 (F2)",
        "difficulty.beginner", "初级",
        "difficulty.intermediate", "中级",
        "difficulty.expert", "高级",
        "difficulty.custom", "自定义",
        "footer.best", "{0} · 最佳 {1} 秒",
        "footer.noBest", "{0} · 尚无最佳成绩",
        "footer.customNoBest", "自定义棋盘 · 不计入最佳成绩",
        "status.exploded", "踩雷了，用时 {0} 秒",
        "status.newRecord", "新纪录！{0} 秒",
        "status.won", "通关！用时 {0} 秒",
        "status.bestCleared", "最佳成绩已重置",
        "menu.appearance", "外观",
        "menu.themeSystem", "跟随系统",
        "menu.themeLight", "浅色",
        "menu.themeDark", "深色",
        "menu.sound", "音效",
        "menu.language", "语言",
        "menu.languageSystem", "跟随系统",
        "menu.resetBestTimes", "重置最佳成绩",
        "menu.about", "关于 Fluent Sweeper",
        "dialog.reset.title", "重置最佳成绩",
        "dialog.reset.body", "所有难度的最佳成绩都会被清除，此操作无法撤销。",
        "dialog.reset.primary", "重置",
        "action.cancel", "取消",
        "dialog.about.tagline", "经典扫雷 · WinUI 3 / Fluent Design 复刻",
        "dialog.about.controls", "左键翻开 · 右键插旗 · 中键或再次点击数字快速展开 · 方向键 + 空格",
        "dialog.about.version", "版本 {0}",
        "action.close", "关闭",
        "custom.title", "自定义棋盘",
        "custom.primary", "开始游戏",
        "custom.cancel", "取消",
        "custom.columns", "列数（宽）",
        "custom.rows", "行数（高）",
        "custom.mines", "雷数",
        "custom.invalid", "参数无效",
        "custom.hint", "提示：列 3–40，行 3–30，雷数最多为格子总数减一；第一次点击永远不会踩雷。",
        "error.rows", "行数需要在 {0}–{1} 之间",
        "error.columns", "列数需要在 {0}–{1} 之间",
        "error.mines", "雷数需要在 1–{0} 之间",
        "nav.game", "游戏",
        "nav.settings", "设置",
        "settings.title", "设置",
        "settings.appearance", "外观",
        "settings.sound", "声音",
        "settings.language", "语言",
        "settings.data", "数据",
        "settings.aboutTitle", "关于",
        "settings.theme", "主题",
        "settings.themeDesc", "跟随 Windows，或指定浅色 / 深色",
        "settings.soundDesc", "翻开、插旗、爆炸与按钮点击音",
        "settings.languageDesc", "默认跟随 Windows 显示语言",
        "settings.resetTitle", "最佳成绩",
        "settings.resetDesc", "清除所有难度的最佳成绩",
        "settings.resetButton", "清除",
        "settings.version", "版本 {0}",
        "settings.viewSource", "在 GitHub 上查看");

    private static readonly Dictionary<string, string> Japanese = Table(
        "app.title", "Fluent Sweeper",
        "board.name", "Fluent Sweeper の盤面",
        "button.newGame", "新しいゲーム (F2)",
        "button.settings", "設定",
        "button.restart", "やり直す (F2)",
        "difficulty.beginner", "初級",
        "difficulty.intermediate", "中級",
        "difficulty.expert", "上級",
        "difficulty.custom", "カスタム",
        "footer.best", "{0} · ベスト {1} 秒",
        "footer.noBest", "{0} · ベスト記録なし",
        "footer.customNoBest", "カスタム盤面 · 記録対象外",
        "status.exploded", "地雷を踏みました — {0} 秒",
        "status.newRecord", "新記録！{0} 秒",
        "status.won", "クリア！{0} 秒",
        "status.bestCleared", "ベスト記録を消去しました",
        "menu.appearance", "外観",
        "menu.themeSystem", "システム設定に従う",
        "menu.themeLight", "ライト",
        "menu.themeDark", "ダーク",
        "menu.sound", "効果音",
        "menu.language", "言語",
        "menu.languageSystem", "システム言語に従う",
        "menu.resetBestTimes", "ベスト記録を消去",
        "menu.about", "Fluent Sweeper について",
        "dialog.reset.title", "ベスト記録を消去",
        "dialog.reset.body", "すべての難易度のベスト記録が消去されます。この操作は取り消せません。",
        "dialog.reset.primary", "消去",
        "action.cancel", "キャンセル",
        "dialog.about.tagline", "クラシック マインスイーパー · WinUI 3 / Fluent Design リメイク",
        "dialog.about.controls", "左クリックで開く · 右クリックで旗 · 数字を中クリックまたは再クリックで一括展開 · 矢印キー + Space",
        "dialog.about.version", "バージョン {0}",
        "action.close", "閉じる",
        "custom.title", "カスタム盤面",
        "custom.primary", "ゲーム開始",
        "custom.cancel", "キャンセル",
        "custom.columns", "列数（幅）",
        "custom.rows", "行数（高さ）",
        "custom.mines", "地雷の数",
        "custom.invalid", "値が正しくありません",
        "custom.hint", "列 3–40、行 3–30、地雷はマス数 − 1 まで。最初のクリックは絶対に地雷になりません。",
        "error.rows", "行数は {0}〜{1} の範囲で指定してください",
        "error.columns", "列数は {0}〜{1} の範囲で指定してください",
        "error.mines", "地雷の数は 1〜{0} の範囲で指定してください",
        "nav.game", "ゲーム",
        "nav.settings", "設定",
        "settings.title", "設定",
        "settings.appearance", "外観",
        "settings.sound", "サウンド",
        "settings.language", "言語",
        "settings.data", "データ",
        "settings.aboutTitle", "このアプリについて",
        "settings.theme", "テーマ",
        "settings.themeDesc", "Windows に従うか、ライト／ダークを選択します",
        "settings.soundDesc", "開示・旗・爆発・ボタンの効果音",
        "settings.languageDesc", "Windows の表示言語に従います",
        "settings.resetTitle", "ベスト記録",
        "settings.resetDesc", "すべての難易度のベスト記録を消去します",
        "settings.resetButton", "消去",
        "settings.version", "バージョン {0}",
        "settings.viewSource", "GitHub で見る");

    private static readonly Dictionary<string, string> French = Table(
        "app.title", "Fluent Sweeper",
        "board.name", "Plateau de Fluent Sweeper",
        "button.newGame", "Nouvelle partie (F2)",
        "button.settings", "Paramètres",
        "button.restart", "Recommencer (F2)",
        "difficulty.beginner", "Débutant",
        "difficulty.intermediate", "Intermédiaire",
        "difficulty.expert", "Expert",
        "difficulty.custom", "Personnalisé",
        "footer.best", "{0} · record {1} s",
        "footer.noBest", "{0} · aucun record",
        "footer.customNoBest", "Plateau personnalisé · hors classement",
        "status.exploded", "Mine touchée — {0} s",
        "status.newRecord", "Nouveau record ! {0} s",
        "status.won", "Terminé en {0} s",
        "status.bestCleared", "Records réinitialisés",
        "menu.appearance", "Apparence",
        "menu.themeSystem", "Suivre le système",
        "menu.themeLight", "Clair",
        "menu.themeDark", "Sombre",
        "menu.sound", "Effets sonores",
        "menu.language", "Langue",
        "menu.languageSystem", "Suivre la langue du système",
        "menu.resetBestTimes", "Réinitialiser les records",
        "menu.about", "À propos de Fluent Sweeper",
        "dialog.reset.title", "Réinitialiser les records",
        "dialog.reset.body", "Les records de tous les niveaux seront effacés. Cette action est irréversible.",
        "dialog.reset.primary", "Réinitialiser",
        "action.cancel", "Annuler",
        "dialog.about.tagline", "Démineur classique · refait avec WinUI 3 / Fluent Design",
        "dialog.about.controls", "Clic gauche pour révéler · clic droit pour poser un drapeau · clic du milieu ou re-clic sur un chiffre pour ouvrir en cascade · flèches + Espace",
        "dialog.about.version", "Version {0}",
        "action.close", "Fermer",
        "custom.title", "Plateau personnalisé",
        "custom.primary", "Lancer la partie",
        "custom.cancel", "Annuler",
        "custom.columns", "Colonnes (largeur)",
        "custom.rows", "Lignes (hauteur)",
        "custom.mines", "Mines",
        "custom.invalid", "Ces valeurs ne conviennent pas",
        "custom.hint", "Colonnes 3–40, lignes 3–30, mines jusqu'à cases − 1. Le premier clic ne tombe jamais sur une mine.",
        "error.rows", "Les lignes doivent être comprises entre {0} et {1}.",
        "error.columns", "Les colonnes doivent être comprises entre {0} et {1}.",
        "error.mines", "Le nombre de mines doit être compris entre 1 et {0}.",
        "nav.game", "Jeu",
        "nav.settings", "Paramètres",
        "settings.title", "Paramètres",
        "settings.appearance", "Apparence",
        "settings.sound", "Son",
        "settings.language", "Langue",
        "settings.data", "Données",
        "settings.aboutTitle", "À propos",
        "settings.theme", "Thème",
        "settings.themeDesc", "Suivre Windows, ou choisir clair ou sombre",
        "settings.soundDesc", "Révélations, drapeaux, explosions et clics",
        "settings.languageDesc", "Suit la langue d'affichage de Windows",
        "settings.resetTitle", "Meilleurs temps",
        "settings.resetDesc", "Efface le meilleur temps de chaque difficulté",
        "settings.resetButton", "Effacer",
        "settings.version", "Version {0}",
        "settings.viewSource", "Voir sur GitHub");

    private static readonly Dictionary<string, string> Korean = Table(
        "app.title", "Fluent Sweeper",
        "board.name", "Fluent Sweeper 보드",
        "button.newGame", "새 게임 (F2)",
        "button.settings", "설정",
        "button.restart", "다시 시작 (F2)",
        "difficulty.beginner", "초급",
        "difficulty.intermediate", "중급",
        "difficulty.expert", "고급",
        "difficulty.custom", "사용자 지정",
        "footer.best", "{0} · 최고 기록 {1}초",
        "footer.noBest", "{0} · 최고 기록 없음",
        "footer.customNoBest", "사용자 지정 보드 · 기록 제외",
        "status.exploded", "지뢰를 밟았습니다 — {0}초",
        "status.newRecord", "신기록! {0}초",
        "status.won", "클리어! {0}초",
        "status.bestCleared", "최고 기록을 지웠습니다",
        "menu.appearance", "모양",
        "menu.themeSystem", "시스템 설정 사용",
        "menu.themeLight", "밝게",
        "menu.themeDark", "어둡게",
        "menu.sound", "효과음",
        "menu.language", "언어",
        "menu.languageSystem", "시스템 언어 사용",
        "menu.resetBestTimes", "최고 기록 지우기",
        "menu.about", "Fluent Sweeper 정보",
        "dialog.reset.title", "최고 기록 지우기",
        "dialog.reset.body", "모든 난이도의 최고 기록이 삭제됩니다. 되돌릴 수 없습니다.",
        "dialog.reset.primary", "지우기",
        "action.cancel", "취소",
        "dialog.about.tagline", "클래식 지뢰 찾기 · WinUI 3 / Fluent Design 리메이크",
        "dialog.about.controls", "왼쪽 클릭으로 열기 · 오른쪽 클릭으로 깃발 · 숫자를 가운데 클릭하거나 다시 클릭하면 한꺼번에 열기 · 방향키 + Space",
        "dialog.about.version", "버전 {0}",
        "action.close", "닫기",
        "custom.title", "사용자 지정 보드",
        "custom.primary", "게임 시작",
        "custom.cancel", "취소",
        "custom.columns", "열 수(너비)",
        "custom.rows", "행 수(높이)",
        "custom.mines", "지뢰 수",
        "custom.invalid", "값이 올바르지 않습니다",
        "custom.hint", "열 3–40, 행 3–30, 지뢰는 칸 수 − 1까지. 첫 클릭은 절대 지뢰가 아닙니다.",
        "error.rows", "행 수는 {0}–{1} 사이여야 합니다.",
        "error.columns", "열 수는 {0}–{1} 사이여야 합니다.",
        "error.mines", "지뢰 수는 1–{0} 사이여야 합니다.",
        "nav.game", "게임",
        "nav.settings", "설정",
        "settings.title", "설정",
        "settings.appearance", "모양",
        "settings.sound", "소리",
        "settings.language", "언어",
        "settings.data", "데이터",
        "settings.aboutTitle", "정보",
        "settings.theme", "테마",
        "settings.themeDesc", "Windows 설정을 따르거나 라이트/다크를 선택합니다",
        "settings.soundDesc", "열기·깃발·폭발·버튼 소리",
        "settings.languageDesc", "Windows 표시 언어를 따릅니다",
        "settings.resetTitle", "최고 기록",
        "settings.resetDesc", "모든 난이도의 최고 기록을 지웁니다",
        "settings.resetButton", "지우기",
        "settings.version", "버전 {0}",
        "settings.viewSource", "GitHub에서 보기");
}
