using System.IO;
using System.Windows;
using MapExTools.Core.Config;
using MapExTools.Core.Game;
using MapExTools.Core.Launch;
using MapExTools.Core.Paths;

namespace MapExTools.Services;

/// <summary>主程序共用的服务与当前配置。</summary>
public sealed class AppServices
{
    public AppServices(UserDataPaths paths, ToolConfigService configService, ToolConfig config)
    {
        Paths = paths;
        ConfigService = configService;
        Config = config;
        Launcher = CreateLauncher(paths);
        Coc = new CocSession(paths);
        RefreshGameLocation();
    }

    public UserDataPaths Paths { get; }

    public ToolConfigService ConfigService { get; }

    public ToolConfig Config { get; }

    /// <summary>启动游戏（计划 2.2）。</summary>
    public GameLauncher Launcher { get; }

    /// <summary>窗口级共享的 MapExtPDX.coc 配置状态与待保存副本。</summary>
    public CocSession Coc { get; }

    /// <summary>游戏与 Steam 的位置。启动时找一次；打开启动页、改手动路径、点启动时重找。</summary>
    public GameLocateResult GameLocation { get; private set; } = GameLocateResult.None;

    public GameLocateResult RefreshGameLocation()
    {
        GameLocation = LocateGame(Paths, Config);
        return GameLocation;
    }

    /// <summary>按配置里的手动路径、注册表里的 Steam、Skyve 的记录找游戏；命令行也用它。</summary>
    public static GameLocateResult LocateGame(UserDataPaths paths, ToolConfig config) =>
        GameLocator.Locate(new GameLocatorInput(config.GameExePath, SteamRegistry.GetSteamDirCandidates(), paths.SkyveFolderSettings));

    public static GameLauncher CreateLauncher(UserDataPaths paths) =>
        new(paths.RunOnceFile, GameProcess.IsRunning, SteamRegistry.IsSteamRunning);

    /// <summary>保存当前配置；失败时弹窗说明，不抛出。</summary>
    public bool TrySaveConfig(Window? owner)
    {
        try
        {
            ConfigService.Save(Config);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            string text = Loc.Instance.Format("Config.SaveFailed", ex.Message);
            if (owner != null)
                MessageBox.Show(owner, text, Loc.Instance["App.Title"], MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                MessageBox.Show(text, Loc.Instance["App.Title"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
