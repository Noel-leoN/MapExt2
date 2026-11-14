using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using MapExTools.Core.Install;
using MapExTools.Core.Paths;

namespace MapExTools.Services;

/// <summary>
/// 运行位置检查与"固定到本地"的界面流程（计划 2.3、§2 第 9 条）。归类、复制与快捷方式在 Core 的
/// <c>Install/</c>，这里负责弹窗与从新位置重新启动。只在玩家点击后执行，不自动执行。
/// </summary>
internal static class InstallLocationService
{
    /// <summary>本次运行的 exe 位置，启动时判定一次。</summary>
    public static InstallLocation Current { get; private set; } = null!;

    /// <summary>
    /// exe 旁边有 MapExTools.dll：框架依赖的多文件构建（开发时的 bin 目录）。"固定到本地"只复制 exe，这种构建复制过去跑不起来。
    /// </summary>
    public static bool IsDevelopmentBuild { get; private set; }

    private static string Language => Loc.Instance.Language;

    public static void Initialize(UserDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        string exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, ToolPinning.ExeName);
        Current = InstallLocation.Classify(ShellFolders.GetLongPath(exe), ShellFolders.Detect(paths.ModsDir));
        IsDevelopmentBuild = Current.HasCompanionDll;
    }

    /// <summary>
    /// 在游戏的 Mods 下运行时弹窗要求移出，可选直接固定到本地。返回 true 表示照常运行；false 表示应退出
    /// （不论是否已从新位置启动）。
    /// </summary>
    public static bool CheckGameModsAtStartup()
    {
        if (Current.Kind != InstallLocationKind.GameMods)
            return true;

        string title = Loc.Instance["App.Title"];
        if (IsDevelopmentBuild)
        {
            MessageBox.Show(Loc.Instance.Format("Install.ModsMoveOut", Current.ExePath), title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        MessageBoxResult answer = MessageBox.Show(
            Loc.Instance.Format("Install.ModsPin", Current.ExePath, ToolPinning.DefaultPinnedDir), title,
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes)
            PinAndStart(null, confirm: false, hasUnsavedSettings: false);
        return false;
    }

    /// <summary>
    /// "固定到本地"：确认 → 复制到固定位置（已有时覆盖更新）→ 建桌面快捷方式 → 从新位置启动。
    /// 返回 true 表示新位置已启动，本进程应退出；取消或失败时返回 false，已弹窗说明原因。
    /// </summary>
    /// <param name="confirm">是否先弹确认框；在 Mods 下启动时的弹窗本身就是确认，不再重复（降级仍会确认）。</param>
    public static bool PinAndStart(Window? owner, bool confirm, bool hasUnsavedSettings)
    {
        if (IsDevelopmentBuild)
        {
            Show(owner, Loc.Instance["Install.PinDevBuild"], MessageBoxImage.Warning);
            return false;
        }

        PinPlan plan;
        try
        {
            plan = ToolPinning.Plan(Current.ExePath, ToolPinning.DefaultPinnedDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Show(owner, Loc.Instance.Format("Install.PinFailed", ex.Message), MessageBoxImage.Warning);
            return false;
        }

        // 固定位置那一份正在运行：覆盖不了，也不该再开一份
        if (plan.TargetExists && !plan.IsSameFile && ToolPinning.IsRunningFrom(plan.TargetPath))
        {
            Show(owner, Loc.Instance.Format("Install.PinRunning", plan.TargetPath), MessageBoxImage.Information);
            return false;
        }

        if (confirm || plan.IsDowngrade)
        {
            MessageBoxResult answer = Ask(owner, InstallText.PinConfirm(plan, hasUnsavedSettings, Language),
                plan.IsDowngrade ? MessageBoxImage.Warning : MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
                return false;
        }

        try
        {
            ToolPinning.Copy(plan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Show(owner, Loc.Instance.Format("Install.PinFailed", ex.Message), MessageBoxImage.Warning);
            return false;
        }

        // 快捷方式建不成不影响使用，说明原因后照常从新位置启动；取不到桌面目录（极少见）时跳过
        if (ToolShortcut.DesktopDir is { Length: > 0 } desktop)
        {
            try
            {
                ToolShortcut.Create(desktop, plan.TargetPath, Loc.Instance["Install.ShortcutDescription"]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or InvalidOperationException)
            {
                Show(owner, Loc.Instance.Format("Install.PinShortcutFailed", ex.Message), MessageBoxImage.Warning);
            }
        }

        // 直接创建进程，不经 ShellExecute：刚刚运行过的同一个文件，不让 SmartScreen 在中途再拦一次
        try
        {
            var start = new ProcessStartInfo(plan.TargetPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(plan.TargetPath)!,
            };
            Process.Start(start)?.Dispose();
            return true;
        }
        catch (Win32Exception ex)
        {
            Show(owner, Loc.Instance.Format("Install.PinStartFailed", plan.TargetPath, ex.Message), MessageBoxImage.Warning);
            return false;
        }
    }

    private static void Show(Window? owner, string text, MessageBoxImage image)
    {
        string title = Loc.Instance["App.Title"];
        if (owner != null)
            MessageBox.Show(owner, text, title, MessageBoxButton.OK, image);
        else
            MessageBox.Show(text, title, MessageBoxButton.OK, image);
    }

    private static MessageBoxResult Ask(Window? owner, string text, MessageBoxImage image)
    {
        string title = Loc.Instance["App.Title"];
        return owner != null
            ? MessageBox.Show(owner, text, title, MessageBoxButton.YesNo, image)
            : MessageBox.Show(text, title, MessageBoxButton.YesNo, image);
    }
}
