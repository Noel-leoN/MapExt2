using System.IO;
using System.Windows;
using MapExTools.Core.Config;
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
    }

    public UserDataPaths Paths { get; }

    public ToolConfigService ConfigService { get; }

    public ToolConfig Config { get; }

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
