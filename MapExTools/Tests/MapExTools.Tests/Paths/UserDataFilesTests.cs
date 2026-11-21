using System.Diagnostics;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Paths;

public sealed class UserDataFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", "UserDataFiles_" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _junctions = [];

    public void Dispose()
    {
        // 先解除所有 Junction 再删除普通文件与目录，避免循环链接导致递归删除失败
        foreach (string link in _junctions)
        {
            if (Directory.Exists(link))
                Directory.Delete(link, recursive: false);
        }

        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void EnumerateFiles_SkipsDotAndTildeEntries_AndMatchesExtensionsCaseInsensitively()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Sub"));
        Directory.CreateDirectory(Path.Combine(_root, ".hidden"));
        Directory.CreateDirectory(Path.Combine(_root, "~backup"));

        File.WriteAllText(Path.Combine(_root, "a.PNG"), "1");
        File.WriteAllText(Path.Combine(_root, "~skip.png"), "2");
        File.WriteAllText(Path.Combine(_root, ".skip.png"), "3");
        File.WriteAllText(Path.Combine(_root, "other.txt"), "4");
        File.WriteAllText(Path.Combine(_root, "Sub", "b.tif"), "5");
        File.WriteAllText(Path.Combine(_root, ".hidden", "c.png"), "6");
        File.WriteAllText(Path.Combine(_root, "~backup", "d.png"), "7");

        string[] files = UserDataFiles.Enumerate(_root, [".png", ".tif"])
            .Select(p => Path.GetRelativePath(_root, p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(["a.PNG", Path.Combine("Sub", "b.tif")], files);
    }

    [Fact]
    public void EnumerateFiles_JunctionSelfLoopAndMutualLoop_TerminatesCleanly()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Skip("Junction 环路测试仅在 Windows 上运行。");

        string dirA = Path.Combine(_root, "A");
        string dirB = Path.Combine(_root, "B");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        File.WriteAllText(Path.Combine(dirA, "one.dll"), "a");
        File.WriteAllText(Path.Combine(dirB, "two.dll"), "b");

        string selfLoop = Path.Combine(dirA, "SelfLoop");
        string aToB = Path.Combine(dirA, "ToB");
        string bToA = Path.Combine(dirB, "ToA");

        if (!TryCreateJunction(selfLoop, dirA) || !TryCreateJunction(aToB, dirB) || !TryCreateJunction(bToA, dirA))
            Assert.Skip("当前环境无法创建 NTFS Junction，跳过目录环路测试。");

        string[] files = UserDataFiles.Enumerate(_root, [".dll"])
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;

        Assert.Equal(["one.dll", "two.dll"], files);
    }

    private bool TryCreateJunction(string linkPath, string targetPath)
    {
        try
        {
            using Process? proc = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (proc == null)
                return false;
            proc.WaitForExit(5000);
            if (proc.ExitCode == 0 && Directory.Exists(linkPath))
            {
                _junctions.Add(linkPath);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
