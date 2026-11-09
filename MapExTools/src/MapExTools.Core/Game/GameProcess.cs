using System.Diagnostics;

namespace MapExTools.Core.Game;

/// <summary>检测游戏进程 <c>Cities2</c> 是否在运行。</summary>
public static class GameProcess
{
    public const string ProcessName = "Cities2";

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
