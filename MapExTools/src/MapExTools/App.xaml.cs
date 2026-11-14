using System.Globalization;
using System.Windows;
using MapExTools.Core.Config;
using MapExTools.Core.Localization;
using MapExTools.Core.Paths;
using MapExTools.Services;

namespace MapExTools;

public partial class App : Application
{
    /// <summary>全局服务，在 <see cref="OnStartup"/> 里建立。</summary>
    public static AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        UserDataPaths paths = GetUserDataOption(e.Args) is { } root ? new UserDataPaths(root) : UserDataPaths.CreateDefault();
        var configService = new ToolConfigService(paths.ToolConfigFile);
        ToolConfigLoadResult load = configService.Load();
        Services = new AppServices(paths, configService, load.Config);

        Loc.Instance.SetLanguage(UiLanguage.Normalize(load.Config.Language)
                                 ?? UiLanguage.FromCulture(CultureInfo.CurrentUICulture));
        ThemeService.Apply(load.Config.Theme);

        // 在游戏的 Mods 下运行时要求移出（计划 §2 第 9 条），不进主窗口
        InstallLocationService.Initialize(paths);
        if (!InstallLocationService.CheckGameModsAtStartup())
        {
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        if (load.Status == ToolConfigLoadStatus.Corrupt)
        {
            MessageBox.Show(window,
                Loc.Instance.Format("Config.Corrupt", load.Error ?? "", load.BackupPath ?? "-"),
                Loc.Instance["Config.CorruptTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// 测试用参数 <c>--user-data &lt;目录&gt;</c> 或 <c>--user-data=&lt;目录&gt;</c>：把游戏用户目录换成别处，
    /// 界面实测时不碰真实的 .coc 与配置。属于计划 2.2 的命令行接口，先行实现这一项，其余参数在 2.2 加。
    /// </summary>
    private static string? GetUserDataOption(string[] args)
    {
        const string name = "--user-data";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && i + 1 < args.Length)
                return args[i + 1];
            if (args[i].StartsWith(name + "=", StringComparison.Ordinal))
                return args[i][(name.Length + 1)..];
        }

        return null;
    }
}
