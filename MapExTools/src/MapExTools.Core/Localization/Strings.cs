namespace MapExTools.Core.Localization;

/// <summary>
/// 三语文案表。每条文案按 <see cref="UiLanguage.All"/> 的顺序给出 en-US、zh-HANS、zh-HANT 三个版本，
/// 占位符用 <c>{0}</c>、<c>{1}</c>，三个版本的占位符要一致（单元测试会检查）。
/// </summary>
public static class Strings
{
    private static readonly Dictionary<string, string[]> s_table = new(StringComparer.Ordinal)
    {
        // === 窗口与标签页 ===
        ["App.Title"] = ["MapExTools", "MapExTools", "MapExTools"],
        ["Tab.Launch"] = ["Launch & Settings", "启动与设定", "啟動與設定"],
        ["Tab.Logs"] = ["Log Diagnostics", "日志诊断", "日誌診斷"],
        ["Tab.Saves"] = ["Saves & Maps", "存档与地图", "存檔與地圖"],
        ["Tab.Heightmaps"] = ["Heightmaps", "高度图", "高度圖"],
        ["Tab.Pending"] = [
            "This page is not implemented yet.",
            "此页面尚未实现。",
            "此頁面尚未實作。",
        ],

        // === 界面设置 ===
        ["Settings.Language"] = ["Language", "语言", "語言"],
        ["Settings.Theme"] = ["Theme", "主题", "主題"],
        ["Theme.System"] = ["System", "跟随系统", "跟隨系統"],
        ["Theme.Light"] = ["Light", "浅色", "淺色"],
        ["Theme.Dark"] = ["Dark", "深色", "深色"],

        // === 状态栏 ===
        ["Status.GameRunning"] = [
            "Cities: Skylines II is running",
            "游戏正在运行",
            "遊戲正在執行",
        ],
        ["Status.GameNotRunning"] = [
            "Cities: Skylines II is not running",
            "游戏未运行",
            "遊戲未執行",
        ],
        ["Status.UserData"] = ["User data: {0}", "用户数据目录：{0}", "使用者資料目錄：{0}"],

        // === 配置文件 ===
        ["Config.CorruptTitle"] = ["Settings file damaged", "配置文件已损坏", "設定檔已損毀"],
        ["Config.Corrupt"] = [
            "MapExTools.json could not be read and the defaults are used instead.\n{0}\nA copy of the damaged file was kept at:\n{1}",
            "无法读取 MapExTools.json，已改用默认值。\n{0}\n损坏的文件已另存一份：\n{1}",
            "無法讀取 MapExTools.json，已改用預設值。\n{0}\n損毀的檔案已另存一份：\n{1}",
        ],
        ["Config.SaveFailed"] = [
            "Could not save MapExTools.json: {0}",
            "保存 MapExTools.json 失败：{0}",
            "儲存 MapExTools.json 失敗：{0}",
        ],

        // === 设定文件 MapExtPDX.coc：格式错误（{0} 为 Coc.Position）与写入失败 ===
        ["Coc.Position"] = ["line {0}, column {1}", "第 {0} 行第 {1} 列", "第 {0} 行第 {1} 欄"],
        ["Coc.Err.StringUnclosed"] = [
            "{0}: the string is not closed",
            "{0}：字符串没有闭合",
            "{0}：字串沒有閉合",
        ],
        ["Coc.Err.BracketUnclosed"] = [
            "{0}: the bracket is not closed",
            "{0}：括号没有闭合",
            "{0}：括號沒有閉合",
        ],
        ["Coc.Err.NoOpenBrace"] = [
            "{0}: missing {{ after block '{1}'",
            "{0}：块 '{1}' 之后缺少 {{",
            "{0}：區塊 '{1}' 之後缺少 {{",
        ],
        ["Coc.Err.KeyExpected"] = [
            "{0}: expected a quoted key",
            "{0}：这里应为带引号的键名",
            "{0}：這裡應為帶引號的鍵名",
        ],
        ["Coc.Err.ColonMissing"] = [
            "{0}: missing colon after key '{1}'",
            "{0}：键 '{1}' 之后缺少冒号",
            "{0}：鍵 '{1}' 之後缺少冒號",
        ],
        ["Coc.Err.ValueMissing"] = [
            "{0}: key '{1}' has no value",
            "{0}：键 '{1}' 缺少取值",
            "{0}：鍵 '{1}' 缺少值",
        ],
        ["Coc.Err.CommaMissing"] = [
            "{0}: missing comma between two entries",
            "{0}：两项之间缺少逗号",
            "{0}：兩項之間缺少逗號",
        ],
        ["Coc.Err.TrailingComma"] = [
            "{0}: extra comma after the last entry",
            "{0}：最后一项后面多了逗号",
            "{0}：最後一項後面多了逗號",
        ],
        ["Coc.Err.BadLiteral"] = [
            "{0}: unrecognized value '{1}'",
            "{0}：无法识别的取值 '{1}'",
            "{0}：無法識別的值 '{1}'",
        ],
        ["Coc.Err.DuplicateKey"] = [
            "{0}: key '{1}' appears more than once; the game cannot read the block",
            "{0}：键 '{1}' 重复出现，游戏会读不进整个块",
            "{0}：鍵 '{1}' 重複出現，遊戲會讀不進整個區塊",
        ],
        ["Coc.Err.InvalidEncoding"] = [
            "The settings file is not valid UTF-8 text.",
            "设定文件不是有效的 UTF-8 文本。",
            "設定檔不是有效的 UTF-8 文字。",
        ],
        ["Coc.Err.FileChanged"] = [
            "The settings file was changed by another program after it was read. Reload it and save again.",
            "设定文件读取后被其他程序改过，请重新读取后再保存。",
            "設定檔讀取後被其他程式改過，請重新讀取後再儲存。",
        ],
        ["Coc.Err.VerifyFailed"] = [
            "Verification after writing failed: {0}",
            "写入结果校验失败：{0}",
            "寫入結果驗證失敗：{0}",
        ],

        // === 日志诊断：会话摘要 ===
        ["Logs.Summary"] = ["Session summary", "会话摘要", "工作階段摘要"],
        ["Logs.Refresh"] = ["Refresh", "刷新", "重新整理"],
        ["Logs.Copy"] = ["Copy diagnostics", "复制诊断摘要", "複製診斷摘要"],
        ["Logs.Copied"] = [
            "Diagnostics copied to the clipboard (Markdown).",
            "诊断摘要已复制到剪贴板（Markdown）。",
            "診斷摘要已複製到剪貼簿（Markdown）。",
        ],
        ["Logs.CopyFailed"] = [
            "Could not copy to the clipboard: {0}",
            "无法复制到剪贴板：{0}",
            "無法複製到剪貼簿：{0}",
        ],
        ["Logs.OpenFolder"] = ["Open log folder", "打开日志目录", "開啟日誌資料夾"],
        ["Logs.FolderMissing"] = ["The folder does not exist: {0}", "目录不存在：{0}", "資料夾不存在：{0}"],
        ["Logs.Title"] = ["MapExTools diagnostics", "MapExTools 诊断摘要", "MapExTools 診斷摘要"],
        ["Logs.Generated"] = ["Generated {0}", "生成于 {0}", "產生於 {0}"],
        ["Logs.Section.LastRun"] = ["Last run", "上次运行", "上次執行"],
        ["Logs.Section.MapExtVersion"] = ["MapExt {0}", "MapExt {0}", "MapExt {0}"],
        ["Logs.Section.Launch"] = ["Launch", "启动", "啟動"],
        ["Logs.GameVersion"] = ["Game version: {0}", "游戏版本：{0}", "遊戲版本：{0}"],
        ["Logs.GameVersionUnknown"] = [
            "Game version: unknown (no Player.log or SceneFlow.log)",
            "游戏版本：未知（没有 Player.log 与 SceneFlow.log）",
            "遊戲版本：未知（沒有 Player.log 與 SceneFlow.log）",
        ],
        ["Logs.End"] = ["Player.log: {0}", "Player.log：{0}", "Player.log：{0}"],
        ["Logs.PrevEnd"] = [
            "The run before (Player-prev.log): {0}",
            "再上一次运行（Player-prev.log）：{0}",
            "再上一次執行（Player-prev.log）：{0}",
        ],
        ["Logs.State.ExitedNormally"] = ["exited normally", "正常退出", "正常結束"],
        ["Logs.State.Crashed"] = ["crashed (Unity crash marker)", "闪退（有 Unity 崩溃标记）", "閃退（有 Unity 當機標記）"],
        ["Logs.State.InProgress"] = ["the game is still running", "游戏还在运行", "遊戲仍在執行"],
        ["Logs.State.NoExitRecord"] = [
            "no normal exit record, the game may have crashed",
            "没有正常退出的记录，可能闪退了",
            "沒有正常結束的紀錄，可能閃退了",
        ],
        ["Logs.CrashHint"] = [
            "If the game closed by itself, check {0}",
            "如果游戏是自己关掉的，请查看 {0}",
            "如果遊戲是自己關掉的，請查看 {0}",
        ],
        ["Logs.CrashLogDefault"] = [
            "logs/crash.log in the game folder",
            "游戏目录下的 logs/crash.log",
            "遊戲資料夾下的 logs/crash.log",
        ],
        ["Logs.Problems"] = [
            "{0} exception or error records in Player.log; the last ones:",
            "Player.log 有 {0} 条异常或错误记录，最后几条：",
            "Player.log 有 {0} 筆例外或錯誤紀錄，最後幾筆：",
        ],
        ["Logs.MapExtMissing"] = [
            "No MapExtPDX.log: MapExt was not loaded in the last run",
            "没有 MapExtPDX.log：上次运行没有加载 MapExt",
            "沒有 MapExtPDX.log：上次執行沒有載入 MapExt",
        ],
        ["Logs.Applied"] = ["Settings in effect: {0}", "本会话生效配置：{0}", "本次生效設定：{0}"],
        ["Logs.AppliedMissing"] = [
            "Settings in effect: not recorded (loading may have failed early)",
            "本会话生效配置：没有记录（可能加载早期就出错了）",
            "本次生效設定：沒有紀錄（可能載入初期就出錯了）",
        ],
        ["Logs.Burst.On"] = ["Burst: enabled", "Burst：已启用", "Burst：已啟用"],
        ["Logs.Burst.Off"] = [
            "Burst: disabled, jobs run in Mono and the game is much slower",
            "Burst：已停用，Job 改走 Mono，运行会明显变慢",
            "Burst：已停用，Job 改走 Mono，執行會明顯變慢",
        ],
        ["Logs.Burst.Unknown"] = [
            "Burst: not recorded (MapExt older than v4.12.0)",
            "Burst：没有记录（MapExt 早于 v4.12.0）",
            "Burst：沒有紀錄（MapExt 早於 v4.12.0）",
        ],
        ["Logs.Conflicts.None"] = [
            "Known conflicting mods: none detected",
            "已知冲突 Mod：未检测到",
            "已知衝突 Mod：未偵測到",
        ],
        ["Logs.Conflicts.NotScanned"] = [
            "Known conflicting mods: not recorded",
            "已知冲突 Mod：没有记录",
            "已知衝突 Mod：沒有紀錄",
        ],
        ["Logs.Conflicts.Found"] = ["Known conflicting mods: {0}", "已知冲突 Mod：{0}", "已知衝突 Mod：{0}"],
        ["Logs.Conflicts.AutoDisabled"] = [
            "Groups disabled for this session: {0}",
            "本会话自动停用：{0}",
            "本次自動停用：{0}",
        ],
        ["Logs.Gate"] = ["Last load check: {0}", "最近一次读档判定：{0}", "最近一次讀檔判定：{0}"],
        ["Logs.GateNone"] = [
            "Last load check: none in this session",
            "最近一次读档判定：本会话没有记录",
            "最近一次讀檔判定：本次沒有紀錄",
        ],
        ["Logs.Counts"] = ["{0} warnings, {1} errors", "警告 {0} 条，错误 {1} 条", "警告 {0} 筆，錯誤 {1} 筆"],
        ["Logs.SceneFlowMissing"] = ["No SceneFlow.log", "没有 SceneFlow.log", "沒有 SceneFlow.log"],
        ["Logs.Arguments"] = ["Launch arguments: {0}", "启动参数：{0}", "啟動參數：{0}"],
        ["Logs.ArgumentsNone"] = ["Launch arguments: none", "启动参数：无", "啟動參數：無"],
        ["Logs.Launcher"] = [
            "Started through the Paradox launcher",
            "经 Paradox 启动器启动",
            "經 Paradox 啟動器啟動",
        ],
        ["Logs.ModsTimeout"] = [
            "This load may have started before subscribed mods finished loading. If the map or simulation looks wrong after entering the city, do not save; quit and load again.",
            "这次读档时订阅的 Mod 可能还没加载；进城后如果地图或模拟异常，不要存档，退出后重新读档。",
            "這次讀檔時訂閱的 Mod 可能還沒載入；進城後如果地圖或模擬異常，不要存檔，結束後重新讀檔。",
        ],

        // === 日志诊断：日志浏览 ===
        ["Logs.Level.All"] = ["All levels", "全部级别", "全部層級"],
        ["Logs.Level.Warning"] = ["Warnings and errors", "警告及以上", "警告及以上"],
        ["Logs.Level.Error"] = ["Errors only", "仅错误", "僅錯誤"],
        ["Logs.Keyword"] = ["Filter", "关键词", "關鍵字"],
        ["Logs.Follow"] = ["Follow new lines", "跟随末尾", "跟隨末尾"],
        ["Logs.Status"] = ["{0} / {1} lines · {2}", "{0} / {1} 行 · {2}", "{0} / {1} 行 · {2}"],
        ["Logs.StatusMissing"] = ["File not found: {0}", "文件不存在：{0}", "檔案不存在：{0}"],
        ["Logs.StatusError"] = ["Could not read {0}: {1}", "无法读取 {0}：{1}", "無法讀取 {0}：{1}"],
        ["Logs.Truncated"] = [
            " (only the last {0} lines are kept)",
            "（只保留最后 {0} 行）",
            "（只保留最後 {0} 行）",
        ],
    };

    /// <summary>全部文案键，供单元测试检查。</summary>
    public static IEnumerable<string> Keys => s_table.Keys;

    /// <summary>取文案；该语言为空时退回英文，键不存在时返回键本身（界面上一眼能看出漏了哪条）。</summary>
    public static string Get(string language, string key)
    {
        if (!s_table.TryGetValue(key, out string[]? values))
            return key;

        string value = values[UiLanguage.IndexOf(language)];
        return string.IsNullOrEmpty(value) ? values[0] : value;
    }

    /// <summary>取某条文案的全部语言版本，供单元测试检查。</summary>
    public static IReadOnlyList<string> GetAll(string key) => s_table[key];
}
