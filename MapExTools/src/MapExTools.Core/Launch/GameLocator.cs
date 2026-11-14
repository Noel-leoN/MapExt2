using System.Text.Json;

namespace MapExTools.Core.Launch;

/// <summary>游戏路径从哪里来。</summary>
public enum GameLocationSource
{
    /// <summary>玩家手动指定（配置里的 <c>GameExePath</c>）。</summary>
    Manual,

    /// <summary>Steam 的库文件与游戏清单。</summary>
    Steam,

    /// <summary>Skyve 的游戏内 Mod 写下的 <c>ModsSettings/Skyve/FolderSettings.json</c>。</summary>
    Skyve,
}

/// <summary>找到的游戏。</summary>
/// <param name="ExePath"><c>Cities2.exe</c> 的完整路径。</param>
/// <param name="IsSteamBuild">
/// 游戏目录里有 <c>Cities2_Data/Plugins/x86_64/steam_api64.dll</c>：Steam 版，直启时要 Steam 在后台运行；
/// 没有时按非 Steam 版（如 Xbox）处理，只能直启。
/// </param>
public sealed record GameLocation(string ExePath, GameLocationSource Source, bool IsSteamBuild)
{
    /// <summary>游戏根目录，直启时的工作目录。</summary>
    public string GameDir => Path.GetDirectoryName(ExePath)!;
}

/// <summary>本机的 Steam。</summary>
/// <param name="Dir">Steam 安装目录。</param>
/// <param name="Exe"><c>steam.exe</c> 的完整路径，<c>-applaunch</c> 用它。</param>
public sealed record SteamInfo(string Dir, string Exe);

/// <summary>定位的输入，与注册表、真实用户目录分开，便于单元测试。</summary>
/// <param name="ManualExePath">配置里手动指定的 <c>Cities2.exe</c>；null 表示自动定位。</param>
/// <param name="SteamDirCandidates">注册表里读到的 Steam 目录，按优先级排列（见 <see cref="SteamRegistry"/>）。</param>
/// <param name="SkyveFolderSettingsPath"><c>{UserData}/ModsSettings/Skyve/FolderSettings.json</c>。</param>
public sealed record GameLocatorInput(
    string? ManualExePath,
    IReadOnlyList<string> SteamDirCandidates,
    string? SkyveFolderSettingsPath);

/// <summary>定位结果。</summary>
/// <param name="Game">找到的游戏；都找不到时为 null。</param>
/// <param name="Steam">找到的 Steam；没装或找不到时为 null。</param>
/// <param name="MissingManualPath">手动指定的路径已不存在（或不是 Cities2.exe）时为该路径，此时已改用自动定位。</param>
public sealed record GameLocateResult(GameLocation? Game, SteamInfo? Steam, string? MissingManualPath)
{
    public static GameLocateResult None { get; } = new(null, null, null);

    /// <summary>能否走 Steam 的两条通道：找到了 Steam，且游戏是 Steam 版。</summary>
    public bool SteamChannelsAvailable => Steam != null && Game is { IsSteamBuild: true };
}

/// <summary>
/// 找 <c>Cities2.exe</c>（计划 2.2）：手动指定的路径优先；其次照 Steam 的库文件 <c>steamapps/libraryfolders.vdf</c>
/// 逐个库找游戏清单 <c>appmanifest_949230.acf</c>，取其中的 <c>installdir</c>；再次读 Skyve 记下的游戏路径。
/// </summary>
public static class GameLocator
{
    public const string ExeName = "Cities2.exe";

    /// <summary>游戏在 Steam 上的 AppID。</summary>
    public const string SteamAppId = "949230";

    private static readonly JsonDocumentOptions s_jsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static GameLocateResult Locate(GameLocatorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        SteamInfo? steam = FindSteam(input.SteamDirCandidates);
        string? missingManual = null;

        if (!string.IsNullOrWhiteSpace(input.ManualExePath))
        {
            string? manual = TryGetFullPath(input.ManualExePath);
            if (manual != null && IsGameExe(manual))
                return new GameLocateResult(CreateLocation(manual, GameLocationSource.Manual), steam, null);
            missingManual = input.ManualExePath;
        }

        GameLocation? game = FindInSteamLibraries(steam) ?? FindFromSkyve(input.SkyveFolderSettingsPath);
        return new GameLocateResult(game, steam, missingManual);
    }

    /// <summary>是否是存在的 <c>Cities2.exe</c>（文件名不分大小写）。</summary>
    public static bool IsGameExe(string path) =>
        string.Equals(Path.GetFileName(path), ExeName, StringComparison.OrdinalIgnoreCase) && File.Exists(path);

    // === Steam ===

    /// <summary>第一个有 <c>steam.exe</c> 的候选目录。</summary>
    private static SteamInfo? FindSteam(IReadOnlyList<string> candidates)
    {
        foreach (string candidate in candidates)
        {
            string? dir = TryGetFullPath(candidate);
            if (dir == null)
                continue;

            string exe = Path.Combine(dir, "steam.exe");
            if (File.Exists(exe))
                return new SteamInfo(GetActualCase(dir), GetActualCase(exe));
        }

        return null;
    }

    private static GameLocation? FindInSteamLibraries(SteamInfo? steam)
    {
        if (steam == null)
            return null;

        foreach (string library in GetLibraries(steam.Dir))
        {
            string manifest = Path.Combine(library, "steamapps", $"appmanifest_{SteamAppId}.acf");
            string? installDir = TryRead(manifest) is { } text ? ParseInstallDir(text) : null;
            if (string.IsNullOrWhiteSpace(installDir))
                continue;

            string? exe = TryGetFullPath(Path.Combine(library, "steamapps", "common", installDir, ExeName));
            if (exe != null && File.Exists(exe))
                return CreateLocation(exe, GameLocationSource.Steam);
        }

        return null;
    }

    /// <summary>
    /// 库目录：先按 <c>libraryfolders.vdf</c> 的顺序（Steam 写的路径大小写正确），最后补上 Steam 目录本身；
    /// 不分大小写去重。库文件读不了时只查 Steam 目录。
    /// </summary>
    private static List<string> GetLibraries(string steamDir)
    {
        var libraries = new List<string>();
        string? vdf = TryRead(Path.Combine(steamDir, "steamapps", "libraryfolders.vdf"));
        if (vdf != null)
        {
            try
            {
                libraries.AddRange(ParseLibraryFolders(vdf));
            }
            catch (FormatException)
            {
            }
        }

        libraries.Add(steamDir);
        return libraries
            .Select(TryGetFullPath)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// <c>libraryfolders.vdf</c> 里的库路径。现行格式每个库是 <c>"0" { "path" "…" … }</c>；
    /// 旧格式直接写 <c>"1" "路径"</c>，同层还有 <c>TimeNextStatsReport</c> 等非数字键，跳过。
    /// </summary>
    /// <exception cref="FormatException">文件格式有误。</exception>
    public static IReadOnlyList<string> ParseLibraryFolders(string text)
    {
        VdfNode? folders = VdfReader.Parse(text)["libraryfolders"];
        if (folders == null || !folders.IsObject)
            return [];

        var paths = new List<string>();
        foreach (VdfNode entry in folders.Children)
        {
            if (entry.Key.Length == 0 || !entry.Key.All(char.IsAsciiDigit))
                continue;

            string? path = entry.IsObject ? entry["path"]?.Value : entry.Value;
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }

        return paths;
    }

    /// <summary>游戏清单 <c>appmanifest_*.acf</c> 里的安装目录名（<c>AppState</c> 下的 <c>installdir</c>）；读不出时为 null。</summary>
    public static string? ParseInstallDir(string text)
    {
        try
        {
            return VdfReader.Parse(text)["AppState"]?["installdir"]?.Value;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // === Skyve ===

    /// <summary>
    /// Skyve 的游戏内 Mod 每次运行时写下 <c>FolderSettings.json</c>，其中 <c>GamePath</c> 是游戏根目录
    /// （<c>Application.dataPath</c> 的上一级）。文件读不了或路径已失效时返回 null。
    /// </summary>
    private static GameLocation? FindFromSkyve(string? folderSettingsPath)
    {
        if (string.IsNullOrEmpty(folderSettingsPath) || TryRead(folderSettingsPath) is not { } json)
            return null;

        string? gameDir = ParseSkyveGamePath(json);
        if (string.IsNullOrWhiteSpace(gameDir))
            return null;

        string? exe = TryGetFullPath(Path.Combine(gameDir, ExeName));
        return exe != null && File.Exists(exe) ? CreateLocation(exe, GameLocationSource.Skyve) : null;
    }

    /// <summary>取 Skyve <c>FolderSettings.json</c> 里的 <c>GamePath</c>；不是有效 JSON 或没有这个字段时为 null。</summary>
    public static string? ParseSkyveGamePath(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json, s_jsonOptions);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("GamePath", out JsonElement path)
                   && path.ValueKind == JsonValueKind.String
                ? path.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // === 辅助 ===

    private static GameLocation CreateLocation(string exePath, GameLocationSource source)
    {
        string exe = GetActualCase(exePath);
        string dir = Path.GetDirectoryName(exe)!;
        bool steamBuild = File.Exists(Path.Combine(dir, "Cities2_Data", "Plugins", "x86_64", "steam_api64.dll"));
        return new GameLocation(exe, source, steamBuild);
    }

    private static string? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? TryGetFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// 换成磁盘上的实际大小写：注册表里的 Steam 路径是全小写加正斜杠（如 <c>d:/games/steam</c>），
    /// 直接拿来显示不好认。逐级按名称查目录项；任何一级查不到就原样返回完整路径。
    /// </summary>
    public static string GetActualCase(string path)
    {
        string full = Path.GetFullPath(path);
        try
        {
            string? root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root))
                return full;

            // 盘符写成大写；UNC 根目录照原样
            string current = root.Length == 3 && root[1] == ':' ? root.ToUpperInvariant() : root;
            foreach (string part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                string? match = Directory.EnumerateFileSystemEntries(current, part).FirstOrDefault();
                if (match == null)
                    return full;
                current = match;
            }

            return current;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return full;
        }
    }
}
