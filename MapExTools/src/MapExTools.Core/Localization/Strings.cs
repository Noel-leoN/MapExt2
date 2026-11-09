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
