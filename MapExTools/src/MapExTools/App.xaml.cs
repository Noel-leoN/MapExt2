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

        UserDataPaths paths = UserDataPaths.CreateDefault();
        var configService = new ToolConfigService(paths.ToolConfigFile);
        ToolConfigLoadResult load = configService.Load();
        Services = new AppServices(paths, configService, load.Config);

        Loc.Instance.SetLanguage(UiLanguage.Normalize(load.Config.Language)
                                 ?? UiLanguage.FromCulture(CultureInfo.CurrentUICulture));
        ThemeService.Apply(load.Config.Theme);

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
}
