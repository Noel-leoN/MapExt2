using System.Diagnostics;

namespace MapExTools.Core.Game;

/// <summary>检测游戏进程 <c>Cities2</c> 是否在运行。</summary>
public static class GameProcess
{
    public const string DefaultProcessName = "Cities2";

    /// <summary>
    /// 检测用的进程名，默认 <see cref="DefaultProcessName"/>。只在启动时由测试参数 <c>--game-process</c> 改一次
    /// （开发版 <c>-GameProcessName</c> 的对应项），用别的进程模拟"游戏运行中"。
    /// </summary>
    public static string ProcessName { get; set; } = DefaultProcessName;

    public static bool IsRunning()
    {
        Process[] processes = Process.GetProcessesByName(ProcessName);
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
}
