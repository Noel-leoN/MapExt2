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

        // === 存档与地图：页面 ===
        ["Saves.Tab.Saves"] = ["Saves ({0})", "存档（{0}）", "存檔（{0}）"],
        ["Saves.Tab.Maps"] = ["Maps ({0})", "地图（{0}）", "地圖（{0}）"],
        ["Saves.Keyword"] = ["Filter", "关键词", "關鍵字"],
        ["Saves.Rescan"] = ["Rescan", "重新扫描", "重新掃描"],
        ["Saves.Header"] = [
            "Mode in settings: {0} · Map Extension Conversion: {1}",
            "设定的模式：{0} · 地图扩展转换：{1}",
            "設定的模式：{0} · 地圖擴展轉換：{1}",
        ],
        ["Saves.HeaderTip"] = [
            "Read from MapExtPDX.coc. Changes take effect the next time the game starts.",
            "取自 MapExtPDX.coc，游戏下次启动时生效。",
            "取自 MapExtPDX.coc，遊戲下次啟動時生效。",
        ],
        ["Saves.On"] = ["on", "开", "開"],
        ["Saves.Off"] = ["off", "关", "關"],
        ["Saves.CocDefault"] = [
            "MapExtPDX.coc not found: MapExt defaults are shown.",
            "没有 MapExtPDX.coc，按 MapExt 的默认设定显示。",
            "沒有 MapExtPDX.coc，依 MapExt 的預設設定顯示。",
        ],
        ["Saves.CocError"] = [
            "Cannot read MapExtPDX.coc: {0}",
            "无法读取 MapExtPDX.coc：{0}",
            "無法讀取 MapExtPDX.coc：{0}",
        ],
        ["Saves.ValidationOff"] = [
            "\"Disable LoadGame Validation\" is on: saves load without mode checks, and no conversion is offered.",
            "已开启“禁止游戏读取存档验证”：读存档不检查模式，也不提供转换。",
            "已開啟「禁止遊戲讀取存檔驗證」：讀存檔不檢查模式，也不提供轉換。",
        ],
        ["Saves.Scanning"] = ["Scanning saves and maps…", "正在扫描存档与地图…", "正在掃描存檔與地圖…"],
        ["Saves.Status"] = [
            "{0} packages: {1} saves, {2} maps, {3} damaged, {4} with a duplicate guid · scanned in {5:0.0} s",
            "共 {0} 个包：存档 {1}、地图 {2}、损坏 {3}、guid 重复 {4} · 扫描用时 {5:0.0} 秒",
            "共 {0} 個包：存檔 {1}、地圖 {2}、損毀 {3}、guid 重複 {4} · 掃描用時 {5:0.0} 秒",
        ],
        ["Saves.ScanFailed"] = ["Scan failed: {0}", "扫描失败：{0}", "掃描失敗：{0}"],
        ["Saves.NoSelection"] = [
            "Select a save or map on the left.",
            "在左侧选择一个存档或地图。",
            "在左側選擇一個存檔或地圖。",
        ],
        ["Saves.NoPreview"] = ["No preview", "无预览图", "無預覽圖"],
        ["Saves.Filter.All"] = ["All", "全部", "全部"],
        ["Saves.Filter.Match"] = ["Match", "匹配", "相符"],
        ["Saves.Filter.Convertible"] = ["Convertible", "可转换", "可轉換"],
        ["Saves.Filter.Mismatch"] = ["Mismatch", "不符", "不符"],
        ["Saves.Filter.Other"] = ["Stretched or unknown", "会拉伸或未知", "會拉伸或未知"],
        ["Saves.Filter.Damaged"] = ["Damaged", "损坏", "損毀"],
        ["Saves.SwitchTo"] = ["Switch to {0}", "切到 {0}", "切換到 {0}"],
        ["Saves.SwitchCurrent"] = ["Already the mode in settings", "已是设定的模式", "已是設定的模式"],
        ["Saves.SwitchNone"] = ["No matching mode", "没有对应的模式", "沒有對應的模式"],
        ["Saves.ShowInExplorer"] = ["Show in Explorer", "在资源管理器中显示", "在檔案總管中顯示"],
        ["Saves.SwitchConfirm"] = [
            "Change MapExt's map size mode from {0} to {1}?\nIt takes effect the next time the game starts; then load the save from the game's main menu.",
            "把 MapExt 的地图尺寸模式从 {0} 改为 {1}？\n游戏下次启动时生效，之后在游戏主菜单里读档。",
            "把 MapExt 的地圖尺寸模式從 {0} 改為 {1}？\n遊戲下次啟動時生效，之後在遊戲主選單裡讀檔。",
        ],
        ["Saves.SwitchDone"] = [
            "Mode set to {0}. It takes effect the next time the game starts.",
            "已改为 {0}，游戏下次启动时生效。",
            "已改為 {0}，遊戲下次啟動時生效。",
        ],
        ["Saves.SwitchGameRunning"] = [
            "The game is running. Quit it first: the game writes its settings back when it exits and would undo this change.",
            "游戏正在运行。请先退出游戏：游戏退出时会写回设定，覆盖这次的修改。",
            "遊戲正在執行。請先結束遊戲：遊戲結束時會寫回設定，覆蓋這次的修改。",
        ],
        ["Saves.SwitchFailed"] = ["Could not change the mode: {0}", "无法切换模式：{0}", "無法切換模式：{0}"],

        // === 存档与地图：模式与关系 ===
        ["Saves.ModeVanilla"] = ["{0} (vanilla)", "{0}（原版）", "{0}（原版）"],
        ["Saves.UnknownCv"] = ["CV={0} (not a current mode)", "CV={0}（不是现行模式）", "CV={0}（不是現行模式）"],
        ["Saves.Tag.Vanilla"] = [
            "Vanilla save (no MapExt tag)",
            "原版存档（没有 MapExt 标记）",
            "原版存檔（沒有 MapExt 標記）",
        ],
        ["Saves.Tag.VanillaShort"] = ["Vanilla", "原版", "原版"],
        ["Saves.Tag.Legacy"] = [
            "Old MapExt save without a mode tag, treated as {0}",
            "旧版 MapExt 存档，没有模式标签，按 {0} 处理",
            "舊版 MapExt 存檔，沒有模式標籤，依 {0} 處理",
        ],
        ["Saves.Tag.LegacyShort"] = ["{0} (old)", "{0}（旧版）", "{0}（舊版）"],
        ["Saves.Tag.Tagged"] = ["{0} (tag CV={1})", "{0}（标签 CV={1}）", "{0}（標籤 CV={1}）"],
        ["Saves.Tag.Unparsable"] = ["Unreadable mode tag", "模式标签无法解析", "模式標籤無法解析"],
        ["Saves.Tag.UnparsableShort"] = ["Bad tag", "标签无效", "標籤無效"],
        ["Saves.Map.Inferred"] = [
            "{0} (inferred from the area, ratio {1:0.###})",
            "{0}（按面积推断，比值 {1:0.###}）",
            "{0}（依面積推斷，比值 {1:0.###}）",
        ],
        ["Saves.Map.Untrusted"] = [
            "Unknown: the area ratio {0:0.###} fits no current mode",
            "未知：面积比值 {0:0.###} 对不上任何现行模式",
            "未知：面積比值 {0:0.###} 對不上任何現行模式",
        ],
        ["Saves.Map.NoArea"] = [
            "Unknown: no area in the metadata",
            "未知：元数据里没有面积",
            "未知：中繼資料裡沒有面積",
        ],
        ["Saves.Map.UnknownShort"] = ["Mode unknown", "模式未知", "模式未知"],
        ["Saves.Map.RatioShort"] = ["Area ratio {0:0.#}", "面积比值 {0:0.#}", "面積比值 {0:0.#}"],
        ["Saves.Rel.Match"] = ["Match", "匹配", "相符"],
        ["Saves.Rel.Convertible"] = ["Convertible", "可转换", "可轉換"],
        ["Saves.Rel.ConvertibleWhenEnabled"] = ["Convertible once enabled", "开启转换后可转换", "開啟轉換後可轉換"],
        ["Saves.Rel.Stretched"] = ["Stretched", "会拉伸", "會拉伸"],
        ["Saves.Rel.Mismatch"] = ["Mismatch", "不符", "不符"],
        ["Saves.Rel.Unchecked"] = ["Not checked", "不校验", "不驗證"],
        ["Saves.Rel.Unknown"] = ["Unknown", "未知", "未知"],
        ["Saves.Rel.Damaged"] = ["Damaged", "损坏", "損毀"],
        ["Saves.Hint.Match"] = [
            "Loads directly in the mode in settings.",
            "在设定的模式下可直接载入。",
            "在設定的模式下可直接載入。",
        ],
        ["Saves.Hint.ConvertibleSave"] = [
            "Loading it shows a conversion prompt.",
            "读档时会弹出转换确认。",
            "讀檔時會彈出轉換確認。",
        ],
        ["Saves.Hint.ConvertibleMap"] = [
            "Starting a new city on it shows a conversion prompt.",
            "用它开新城时会弹出转换确认。",
            "用它開新城時會彈出轉換確認。",
        ],
        ["Saves.Hint.ConvertibleWhenEnabled"] = [
            "Loading it is blocked now. Turn on \"Map Extension Conversion\" in MapExt settings to get a conversion prompt instead.",
            "现在读档会被拦下；在 MapExt 设置里开启“地图扩展转换”后，读档时会提供转换。",
            "現在讀檔會被攔下；在 MapExt 設定裡開啟「地圖擴展轉換」後，讀檔時會提供轉換。",
        ],
        ["Saves.Hint.Stretched"] = [
            "A vanilla map in an extended mode: the terrain is stretched, and the game warns before starting.",
            "原版地图在扩展模式下开新城，地形会被拉伸，游戏会先弹出警告。",
            "原版地圖在擴展模式下開新城，地形會被拉伸，遊戲會先彈出警告。",
        ],
        ["Saves.Hint.StretchedConvertible"] = [
            "Turn on \"Map Extension Conversion\" to convert it instead.",
            "开启“地图扩展转换”后可改为转换。",
            "開啟「地圖擴展轉換」後可改為轉換。",
        ],
        ["Saves.Hint.MismatchSave"] = [
            "Loading it is blocked in the mode in settings.",
            "在设定的模式下读档会被拦下。",
            "在設定的模式下讀檔會被攔下。",
        ],
        ["Saves.Hint.MismatchMap"] = [
            "Starting a new city on it is blocked in the mode in settings.",
            "在设定的模式下用它开新城会被拦下。",
            "在設定的模式下用它開新城會被攔下。",
        ],
        ["Saves.Hint.Unchecked"] = [
            "The mode does not match, but \"Disable LoadGame Validation\" is on: the game loads it anyway, which can damage the city.",
            "模式不符，但开启了“禁止游戏读取存档验证”：游戏不拦，直接载入，可能损坏城市。",
            "模式不符，但開啟了「禁止遊戲讀取存檔驗證」：遊戲不攔，直接載入，可能損壞城市。",
        ],
        ["Saves.Hint.UnknownMap"] = [
            "The area fits no current mode: the game decides by the actual terrain size after loading.",
            "面积对不上任何现行模式：载入后由游戏按地形实际尺寸判定。",
            "面積對不上任何現行模式：載入後由遊戲依地形實際尺寸判定。",
        ],
        ["Saves.Hint.UnknownSettings"] = [
            "The mode in settings is unknown.",
            "读不出设定的模式。",
            "讀不出設定的模式。",
        ],

        // === 存档与地图：读不出来的包 ===
        ["Saves.Problem.Unreadable"] = ["Cannot open the file: {0}", "无法打开文件：{0}", "無法開啟檔案：{0}"],
        ["Saves.Problem.Corrupt"] = ["Not a valid package (ZIP): {0}", "不是有效的包（ZIP）：{0}", "不是有效的包（ZIP）：{0}"],
        ["Saves.Problem.Empty"] = ["Empty package: it contains nothing", "空包：里面什么都没有", "空包：裡面什麼都沒有"],
        ["Saves.Problem.NoMetadata"] = [
            "No save or map metadata in the package",
            "包里没有存档或地图元数据",
            "包裡沒有存檔或地圖中繼資料",
        ],
        ["Saves.Problem.BadMetadata"] = ["The metadata cannot be parsed: {0}", "元数据无法解析：{0}", "中繼資料無法解析：{0}"],

        // === 存档与地图：详情 ===
        ["Saves.D.City"] = ["City", "城市", "城市"],
        ["Saves.D.Population"] = ["Population", "人口", "人口"],
        ["Saves.D.Money"] = ["Money", "资金", "資金"],
        ["Saves.D.MapUsed"] = ["Map", "地图", "地圖"],
        ["Saves.D.MapName"] = ["Map name", "地图名", "地圖名"],
        ["Saves.D.Area"] = ["Area", "面积", "面積"],
        ["Saves.D.Theme"] = ["Theme", "主题", "主題"],
        ["Saves.D.Mode"] = ["MapExt mode", "MapExt 模式", "MapExt 模式"],
        ["Saves.D.Relation"] = ["With the mode in settings", "与设定模式", "與設定模式"],
        ["Saves.D.AutoSave"] = ["Autosave", "自动存档", "自動存檔"],
        ["Saves.D.Modified"] = ["Modified", "修改时间", "修改時間"],
        ["Saves.D.Size"] = ["Size", "大小", "大小"],
        ["Saves.D.File"] = ["File", "文件", "檔案"],
        ["Saves.D.Guid"] = ["Metadata guid", "元数据 guid", "中繼資料 guid"],
        ["Saves.D.Duplicate"] = ["Same guid as", "guid 相同的包", "guid 相同的包"],
        ["Saves.D.Problem"] = ["Problem", "问题", "問題"],
        ["Saves.D.Preview"] = ["Preview", "预览图", "預覽圖"],
        ["Saves.Yes"] = ["Yes", "是", "是"],
        ["Saves.Preview.None"] = ["The package has no preview", "包里没有预览图", "包裡沒有預覽圖"],
        ["Saves.Preview.Missing"] = [
            "Missing: no entry with guid {0} in the package",
            "缺失：包里没有 guid 为 {0} 的条目",
            "缺失：包裡沒有 guid 為 {0} 的條目",
        ],
        ["Saves.Preview.Decoded"] = ["{0}×{1}, version {2}, {3}", "{0}×{1}，版本 {2}，{3}", "{0}×{1}，版本 {2}，{3}"],
        ["Saves.Preview.Zstd"] = ["ZSTD-compressed", "ZSTD 压缩", "ZSTD 壓縮"],
        ["Saves.Preview.Raw"] = ["uncompressed", "未压缩", "未壓縮"],
        ["Saves.Preview.Placeholder"] = ["1×1 placeholder", "1×1 占位图", "1×1 佔位圖"],
        ["Saves.Preview.UnsupportedFormat"] = [
            "{0}×{1}, pixel format {2}: not supported",
            "{0}×{1}，像素格式 {2}，不支持显示",
            "{0}×{1}，像素格式 {2}，不支援顯示",
        ],
        ["Saves.Preview.UnsupportedLayout"] = [
            "Not a single-layer 2D texture",
            "不是单层二维贴图",
            "不是單層二維貼圖",
        ],
        ["Saves.Preview.Corrupt"] = ["Damaged: {0}", "已损坏：{0}", "已損毀：{0}"],
        ["Saves.Preview.Loading"] = ["Loading…", "读取中…", "讀取中…"],
        ["Saves.Preview.Error"] = ["Cannot read: {0}", "无法读取：{0}", "無法讀取：{0}"],

        // === 高度图：页面 ===
        ["Heightmaps.Folder"] = ["Heightmaps folder: {0}", "高度图目录：{0}", "高度圖目錄：{0}"],
        ["Heightmaps.Open"] = ["Open file…", "打开文件…", "開啟檔案…"],
        ["Heightmaps.Refresh"] = ["Refresh", "刷新", "重新整理"],
        ["Heightmaps.OpenFolder"] = ["Open folder", "打开目录", "開啟資料夾"],
        ["Heightmaps.FolderMissing"] = ["The folder does not exist: {0}", "目录不存在：{0}", "資料夾不存在：{0}"],
        ["Heightmaps.FileFilter"] = [
            "Images (*.png;*.tif;*.tiff;*.jpg;*.jpeg)|*.png;*.tif;*.tiff;*.jpg;*.jpeg|All files (*.*)|*.*",
            "图片 (*.png;*.tif;*.tiff;*.jpg;*.jpeg)|*.png;*.tif;*.tiff;*.jpg;*.jpeg|所有文件 (*.*)|*.*",
            "圖片 (*.png;*.tif;*.tiff;*.jpg;*.jpeg)|*.png;*.tif;*.tiff;*.jpg;*.jpeg|所有檔案 (*.*)|*.*",
        ],
        ["Heightmaps.NoSelection"] = [
            "Select a heightmap on the left, or open or drop an image file.",
            "在左侧选择一张高度图，或打开、拖入图片文件。",
            "在左側選擇一張高度圖，或開啟、拖入圖片檔案。",
        ],
        ["Heightmaps.Status"] = [
            "{0} images: {1} can be imported, {2} cannot",
            "共 {0} 张图：可导入 {1}、不可导入 {2}",
            "共 {0} 張圖：可匯入 {1}、不可匯入 {2}",
        ],
        ["Heightmaps.Scanning"] = ["Reading the Heightmaps folder…", "正在读取高度图目录…", "正在讀取高度圖目錄…"],
        ["Heightmaps.View"] = ["View", "视图", "檢視"],
        ["Heightmaps.View.Raw"] = ["Grayscale (raw)", "原始灰度", "原始灰階"],
        ["Heightmaps.View.Stretched"] = ["Stretched contrast", "自动拉伸", "自動拉伸"],
        ["Heightmaps.View.Layered"] = ["Layered by elevation", "分层着色", "分層著色"],
        ["Heightmaps.HeightScale"] = ["Height scale (m)", "高度比例（米）", "高度比例（公尺）"],
        ["Heightmaps.SeaLevel"] = ["Sea level (m)", "海平面（米）", "海平面（公尺）"],
        ["Heightmaps.Fit"] = ["Fit", "适应窗口", "符合視窗"],
        ["Heightmaps.ActualPixels"] = ["Actual pixels", "实际像素", "實際像素"],
        ["Heightmaps.Zoom"] = ["{0:0}%", "{0:0}%", "{0:0}%"],
        ["Heightmaps.Legend"] = ["Legend", "图例", "圖例"],
        ["Heightmaps.Decoding"] = ["Decoding…", "解码中…", "解碼中…"],
        ["Heightmaps.BadHeightScale"] = [
            "The height scale must be between 200 and 10000 m; the previous value is kept.",
            "高度比例须在 200–10000 米之间，仍用上一个值。",
            "高度比例須在 200–10000 公尺之間，仍用上一個值。",
        ],
        ["Heightmaps.BadSeaLevel"] = [
            "The sea level must be a number; the previous value is kept.",
            "海平面须为数字，仍用上一个值。",
            "海平面須為數字，仍用上一個值。",
        ],

        // === 高度图：格式与能否导入 ===
        ["Heightmaps.Kind.Gray16"] = ["16-bit grayscale", "16 位灰度", "16 位元灰階"],
        ["Heightmaps.Kind.Rgba16"] = [
            "16-bit RGBA (the game uses the R channel)",
            "16 位 RGBA（游戏取 R 通道）",
            "16 位元 RGBA（遊戲取 R 通道）",
        ],
        ["Heightmaps.Kind.Bits"] = ["{0}-bit, {1} channel(s)", "{0} 位 · {1} 通道", "{0} 位元 · {1} 通道"],
        ["Heightmaps.Kind.Float"] = ["{0}-bit floating point, {1} channel(s)", "{0} 位浮点 · {1} 通道", "{0} 位元浮點 · {1} 通道"],
        ["Heightmaps.Ok"] = ["Can be imported", "可以导入游戏", "可以匯入遊戲"],
        ["Heightmaps.OkShort"] = ["OK", "可导入", "可匯入"],
        ["Heightmaps.WrongSize"] = [
            "Wrong size: the game needs 4096×4096",
            "尺寸不符：游戏要求 4096×4096",
            "尺寸不符：遊戲要求 4096×4096",
        ],
        ["Heightmaps.WrongSizeShort"] = ["Wrong size", "尺寸不符", "尺寸不符"],
        ["Heightmaps.WrongFormat"] = [
            "Wrong format: the game needs 16-bit grayscale or 16-bit RGBA",
            "格式不符：游戏要求 16 位灰度或 16 位 RGBA",
            "格式不符：遊戲要求 16 位元灰階或 16 位元 RGBA",
        ],
        ["Heightmaps.WrongFormatShort"] = ["Wrong format", "格式不符", "格式不符"],
        ["Heightmaps.Unrecognized"] = ["Unrecognized image file", "认不出的图片文件", "無法辨識的圖片檔"],
        ["Heightmaps.ReadError"] = ["Cannot read: {0}", "无法读取：{0}", "無法讀取：{0}"],
        ["Heightmaps.TooLarge"] = [
            "Larger than 4096×4096: the content is not shown.",
            "大于 4096×4096，不显示内容。",
            "大於 4096×4096，不顯示內容。",
        ],
        ["Heightmaps.DecodeFailed"] = ["Could not decode the image: {0}", "无法解码图片：{0}", "無法解碼圖片：{0}"],

        // === 高度图：读数与图例 ===
        ["Heightmaps.Size"] = ["{0}×{1} · {2}", "{0}×{1} · {2}", "{0}×{1} · {2}"],
        ["Heightmaps.RawRange"] = ["Raw values {0} – {1}", "原始值 {0} – {1}", "原始值 {0} – {1}"],
        ["Heightmaps.ElevationRange"] = [
            "Elevation {0:N1} – {1:N1} m (mean {2:N1} m)",
            "海拔 {0:N1} – {1:N1} 米（平均 {2:N1} 米）",
            "海拔 {0:N1} – {1:N1} 公尺（平均 {2:N1} 公尺）",
        ],
        ["Heightmaps.Hover"] = [
            "x {0}, y {1} · raw {2} · elevation {3:N1} m",
            "x {0}，y {1} · 原始值 {2} · 海拔 {3:N1} 米",
            "x {0}，y {1} · 原始值 {2} · 海拔 {3:N1} 公尺",
        ],
        ["Heightmaps.AboveSea"] = [" · {0:N1} m above sea level", " · 高出海平面 {0:N1} 米", " · 高出海平面 {0:N1} 公尺"],
        ["Heightmaps.BelowSea"] = [" · {0:N1} m below sea level", " · 低于海平面 {0:N1} 米", " · 低於海平面 {0:N1} 公尺"],
        ["Heightmaps.Legend.Band"] = ["{0:N0} – {1:N0} m", "{0:N0} – {1:N0} 米", "{0:N0} – {1:N0} 公尺"],
        ["Heightmaps.Legend.Coast"] = [
            "Coastline (sea level {0:N1} m)",
            "海岸线（海平面 {0:N1} 米）",
            "海岸線（海平面 {0:N1} 公尺）",
        ],
        ["Heightmaps.Legend.Black"] = ["Black: {0:N0} m", "黑：{0:N0} 米", "黑：{0:N0} 公尺"],
        ["Heightmaps.Legend.White"] = ["White: {0:N0} m", "白：{0:N0} 米", "白：{0:N0} 公尺"],
        ["Heightmaps.Legend.BlackAtMost"] = ["Black: {0:N0} m and below", "黑：{0:N0} 米及以下", "黑：{0:N0} 公尺及以下"],
        ["Heightmaps.Legend.WhiteAtLeast"] = ["White: {0:N0} m and above", "白：{0:N0} 米及以上", "白：{0:N0} 公尺及以上"],
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
