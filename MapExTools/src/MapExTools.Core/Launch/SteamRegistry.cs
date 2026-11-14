using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace MapExTools.Core.Launch;

/// <summary>从注册表找 Steam 目录，以及检查 Steam 是否在运行。</summary>
[SupportedOSPlatform("windows")]
public static class SteamRegistry
{
    /// <summary>
    /// 候选的 Steam 目录，按优先级：当前用户的 <c>HKCU\Software\Valve\Steam\SteamPath</c>（与 Skyve 相同，
    /// 值是全小写加正斜杠），再是安装程序写的 <c>HKLM\SOFTWARE\WOW6432Node\Valve\Steam\InstallPath</c>。
    /// </summary>
    public static IReadOnlyList<string> GetSteamDirCandidates()
    {
        var dirs = new List<string>();
        Add(dirs, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        Add(dirs, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        return dirs;
    }

    /// <summary>Steam 客户端（<c>steam.exe</c>）是否在运行。直启 Steam 版游戏时它必须在后台。</summary>
    public static bool IsSteamRunning()
    {
        Process[] processes = Process.GetProcessesByName("steam");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (Process p in processes)
                p.Dispose();
        }
    }

    private static void Add(List<string> dirs, RegistryKey hive, string subKey, string name)
    {
        try
        {
            using RegistryKey? key = hive.OpenSubKey(subKey);
            if (key?.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value))
                dirs.Add(value);
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            // 读不了就跳过，按下一个来源找
        }
    }
}
