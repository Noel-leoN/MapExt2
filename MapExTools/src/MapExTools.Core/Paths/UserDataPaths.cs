using MapExtPDX;

namespace MapExTools.Core.Paths;

/// <summary>
/// 游戏用户数据目录（<c>{UserData}</c>）下的各个路径。根目录可以替换，供单元测试与命令行 <c>--user-data</c> 使用。
/// </summary>
public sealed class UserDataPaths
{
    public UserDataPaths(string root)
    {
        Root = Path.GetFullPath(root);
    }

    /// <summary>默认根目录：<c>%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II</c>。</summary>
    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Colossal Order", "Cities Skylines II");

    public static UserDataPaths CreateDefault() => new(DefaultRoot);

    public string Root { get; }

    /// <summary>MapExt 的设定文件 <c>ModsSettings/MapExtPDX/MapExtPDX.coc</c>。</summary>
    public string CocFile => Combine(StartupKeys.CocRelativePath);

    /// <summary>工具与 Mod 共用的数据目录 <c>ModsData/MapExt2</c>。</summary>
    public string ToolDataDir => Combine("ModsData/MapExt2");

    /// <summary>工具配置 <c>ModsData/MapExt2/MapExTools.json</c>：与 exe 位置无关，覆盖 exe 升级不丢配置。</summary>
    public string ToolConfigFile => Combine("ModsData/MapExt2/MapExTools.json");

    public string LogsDir => Combine("Logs");

    public string MapExtLog => Combine("Logs/MapExtPDX.log");

    /// <summary>游戏 <c>SceneFlow</c> 日志器的输出：命令行、启动配置、读档流程（不进 Player.log）。</summary>
    public string SceneFlowLog => Combine("Logs/SceneFlow.log");

    public string PlayerLog => Combine("Player.log");

    public string PlayerPrevLog => Combine("Player-prev.log");

    public string SavesDir => Combine("Saves");

    public string MapsDir => Combine("Maps");

    public string HeightmapsDir => Combine("Heightmaps");

    /// <summary>本地 Mod 目录。工具的 exe 不能放在这里，否则会被游戏当作 Mod 扫描。</summary>
    public string ModsDir => Combine("Mods");

    /// <summary>PDX SDK 本地模组与 Playset 配置缓存目录 <c>.cache/Mods</c>。</summary>
    public string ModsCacheDir => Combine(".cache/Mods");

    /// <summary>PDX SDK 激活 Playset 与启用模组列表 <c>.cache/Mods/playset_config.json</c>（只读）。</summary>
    public string PlaysetConfigFile => Combine(".cache/Mods/playset_config.json");

    /// <summary>PDX SDK 订阅与本地源存储根路径配置 <c>.cache/Mods/mod_directory.json</c>（只读）。</summary>
    public string ModsDirectoryFile => Combine(".cache/Mods/mod_directory.json");

    /// <summary>PDX SDK 模组商店显示名与版本号缓存 <c>.cache/Mods/pdx_mods_cache.json</c>（只读）。</summary>
    public string PdxModsCacheFile => Combine(".cache/Mods/pdx_mods_cache.json");

    /// <summary>下次启动的一次性参数：游戏启动时读取后删除。</summary>
    public string RunOnceFile => Combine("runOnce.txt");

    /// <summary>Skyve 的游戏内 Mod 记下的游戏路径与平台，找游戏的第三顺位（只读）。</summary>
    public string SkyveFolderSettings => Combine("ModsSettings/Skyve/FolderSettings.json");

    private string Combine(string relative) =>
        Path.GetFullPath(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));
}
