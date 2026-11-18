using MapExTools.Core.Install;

namespace MapExTools.Tests.Install;

/// <summary>"固定到本地"的预检与复制：新建、内容相同不复制、覆盖更新、降级提醒、目标被占用时原样保留（计划 2.3）。</summary>
public sealed class ToolPinningTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public ToolPinningTests()
    {
        Directory.CreateDirectory(_root);
    }

    private string PinnedDir => Path.Combine(_root, "Programs", "MapExTools");

    private string TargetPath => Path.Combine(PinnedDir, ToolPinning.ExeName);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string WriteSource(byte[] content, string name = "MapExTools.exe")
    {
        string dir = Path.Combine(_root, "Downloads");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private void WriteTarget(byte[] content)
    {
        Directory.CreateDirectory(PinnedDir);
        File.WriteAllBytes(TargetPath, content);
    }

    [Fact]
    public void Plan_NoTarget_CopiesUnderFixedName()
    {
        string source = WriteSource([1, 2, 3], "MapExTools (1).exe");

        PinPlan plan = ToolPinning.Plan(source, PinnedDir);
        Assert.False(plan.TargetExists);
        Assert.True(plan.NeedsCopy);
        Assert.False(plan.ReplacesExisting);
        Assert.Equal(TargetPath, plan.TargetPath);

        ToolPinning.Copy(plan);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(TargetPath));
        Assert.Equal([ToolPinning.ExeName], Directory.GetFiles(PinnedDir).Select(Path.GetFileName));
    }

    [Fact]
    public void Plan_SameContent_DoesNotCopy()
    {
        string source = WriteSource([5, 6, 7, 8]);
        WriteTarget([5, 6, 7, 8]);
        DateTime before = File.GetLastWriteTimeUtc(TargetPath);

        PinPlan plan = ToolPinning.Plan(source, PinnedDir);
        Assert.True(plan.SameContent);
        Assert.False(plan.NeedsCopy);

        ToolPinning.Copy(plan);
        Assert.Equal(before, File.GetLastWriteTimeUtc(TargetPath));
    }

    [Fact]
    public void Plan_DifferentContent_ReplacesExisting()
    {
        string source = WriteSource([9, 9, 9, 9]);
        WriteTarget([1, 1, 1, 1]);

        PinPlan plan = ToolPinning.Plan(source, PinnedDir);
        Assert.True(plan.ReplacesExisting);

        ToolPinning.Copy(plan);
        Assert.Equal([9, 9, 9, 9], File.ReadAllBytes(TargetPath));
        Assert.Single(Directory.GetFiles(PinnedDir));
    }

    [Fact]
    public void Plan_RunningFromTarget_IsSameFile()
    {
        WriteTarget([1]);

        PinPlan plan = ToolPinning.Plan(TargetPath.ToUpperInvariant(), PinnedDir);
        Assert.True(plan.IsSameFile);
        Assert.False(plan.NeedsCopy);
    }

    [Fact]
    public void Copy_TargetInUse_KeepsExistingAndCleansUp()
    {
        string source = WriteSource([2, 2]);
        WriteTarget([1, 1]);
        PinPlan plan = ToolPinning.Plan(source, PinnedDir);

        // 不共享地打开目标，模拟固定位置那一份正在运行
        using (new FileStream(TargetPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Exception ex = Assert.ThrowsAny<Exception>(() => ToolPinning.Copy(plan));
            Assert.True(ex is IOException or UnauthorizedAccessException, ex.GetType().Name);
        }

        Assert.Equal([1, 1], File.ReadAllBytes(TargetPath));
        Assert.Equal([ToolPinning.ExeName], Directory.GetFiles(PinnedDir).Select(Path.GetFileName));
    }

    [Fact]
    public void Plan_ReadsFileVersions()
    {
        // 本工程的程序集带版本资源（Directory.Build.props 的 1.1.0）；纯数据文件没有
        string assembly = typeof(ToolPinning).Assembly.Location;
        WriteTarget([0]);

        PinPlan plan = ToolPinning.Plan(assembly, PinnedDir);
        Assert.Equal(new Version(1, 1, 0, 0), plan.SourceVersion);
        Assert.Null(plan.TargetVersion);
        Assert.False(plan.IsDowngrade);
    }

    [Theory]
    [InlineData("1.0.0.0", "1.1.0.0", true)]
    [InlineData("1.1.0.0", "1.0.0.0", false)]
    [InlineData("1.0.0.0", "1.0.0.0", false)]
    [InlineData(null, "1.1.0.0", false)]
    [InlineData("1.0.0.0", null, false)]
    public void IsDowngrade_WhenTargetIsNewer(string? source, string? target, bool expected)
    {
        var plan = new PinPlan(@"D:\Downloads\MapExTools.exe", @"C:\P\MapExTools.exe", TargetExists: true, SameContent: false,
            source == null ? null : Version.Parse(source), target == null ? null : Version.Parse(target));

        Assert.Equal(expected, plan.IsDowngrade);
    }

    [Fact]
    public void IsDowngrade_SameContent_IsFalse()
    {
        var plan = new PinPlan(@"D:\Downloads\MapExTools.exe", @"C:\P\MapExTools.exe", TargetExists: true, SameContent: true,
            new Version(1, 0), new Version(2, 0));

        Assert.False(plan.IsDowngrade);
    }

    [Fact]
    public void ContentEquals_SameLengthDifferentBytes()
    {
        string a = WriteSource([1, 2, 3], "a.bin");
        string b = WriteSource([1, 2, 4], "b.bin");
        string c = WriteSource([1, 2, 3], "c.bin");

        Assert.False(ToolPinning.ContentEquals(a, b));
        Assert.True(ToolPinning.ContentEquals(a, c));
    }

    [Fact]
    public void IsRunningFrom_UnusedPath_IsFalse()
    {
        Assert.False(ToolPinning.IsRunningFrom(TargetPath));
    }
}
