using System.Runtime.Versioning;
using MapExTools.Core.Install;

namespace MapExTools.Tests.Install;

/// <summary>
/// 快捷方式读写与完整性标签（计划 2.3）。都在临时目录里做，不碰真实桌面；
/// 用可继承的 Low 标签目录模拟 <c>AppData\LocalLow</c>，不往真实的 LocalLow 写文件。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ShellIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public ShellIntegrationTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string WriteFile(string relative, byte[]? content = null)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content ?? [0]);
        return path;
    }

    // === ShellLink ===

    [Fact]
    public void ShellLink_SaveAndRead_RoundTrip()
    {
        string exe = WriteFile(Path.Combine("工具 目录", "MapExTools.exe"));
        string link = Path.Combine(_root, "桌面", "MapExTools.lnk");
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        var info = new ShellLinkInfo(exe, "--lang zh-HANS", Path.GetDirectoryName(exe)!, exe, 0, "MapExt 配套工具：启动设定");

        ShellLink.Save(link, info);
        ShellLinkInfo? read = ShellLink.TryRead(link);

        Assert.NotNull(read);
        Assert.Equal(info.TargetPath, read.TargetPath, ignoreCase: true);
        Assert.Equal(info.Arguments, read.Arguments);
        Assert.Equal(info.WorkingDirectory, read.WorkingDirectory, ignoreCase: true);
        Assert.Equal(info.IconPath, read.IconPath, ignoreCase: true);
        Assert.Equal(0, read.IconIndex);
        Assert.Equal(info.Description, read.Description);
    }

    [Fact]
    public void ShellLink_TryRead_MissingOrGarbage_IsNull()
    {
        Assert.Null(ShellLink.TryRead(Path.Combine(_root, "none.lnk")));
        Assert.Null(ShellLink.TryRead(WriteFile("garbage.lnk", "not a shortcut"u8.ToArray())));
    }

    // === FileIntegrity ===

    [Theory]
    [InlineData("S:(ML;;NW;;;LW)", FileIntegrity.LowRid)]
    [InlineData("S:AI(ML;ID;NW;;;LW)", FileIntegrity.LowRid)]
    [InlineData("S:(ML;;NW;;;ME)", FileIntegrity.MediumRid)]
    [InlineData("S:(ML;;NW;;;S-1-16-4096)", FileIntegrity.LowRid)]
    [InlineData("S:(ML;;NW;;;HI)", 0x3000)]
    [InlineData("S:(AU;SA;FA;;;WD)", null)]
    [InlineData("", null)]
    public void ParseLabelRid(string sddl, int? expected)
    {
        Assert.Equal(expected, FileIntegrity.ParseLabelRid(sddl));
    }

    [Fact]
    public void Integrity_FileInLowFolder_InheritsLow_AndCanBeRaised()
    {
        // 目录带可继承的 Low 标签（同 LocalLow），在其中新建的文件继承 Low
        string lowDir = Path.Combine(_root, "LocalLow");
        Directory.CreateDirectory(lowDir);
        FileIntegrity.SetLabel(lowDir, FileIntegrity.LowRid, inheritable: true);
        string exe = WriteFile(Path.Combine("LocalLow", "Tools", "MapExTools.exe"));

        Assert.Equal(FileIntegrity.LowRid, FileIntegrity.GetLabelRid(exe));

        Assert.True(ToolShortcut.RaiseToMediumIfLow(exe));
        Assert.Equal(FileIntegrity.MediumRid, FileIntegrity.GetLabelRid(exe));

        // 显式标签取代继承来的 Low，标签里只剩一条
        string sddl = FileIntegrity.GetLabelSddl(exe);
        Assert.DoesNotContain(";LW)", sddl, StringComparison.Ordinal);
        Assert.False(ToolShortcut.RaiseToMediumIfLow(exe));
    }

    [Fact]
    public void Integrity_NormalFile_IsLeftAlone()
    {
        string exe = WriteFile("MapExTools.exe");

        Assert.Null(FileIntegrity.GetLabelRid(exe));
        Assert.False(ToolShortcut.RaiseToMediumIfLow(exe));
        Assert.Equal(string.Empty, FileIntegrity.GetLabelSddl(exe));
    }

    // === ToolShortcut ===

    [Fact]
    public void Shortcut_CreateInspectDelete()
    {
        string desktop = Path.Combine(_root, "Desktop");
        string exe = WriteFile(Path.Combine("Tools", "MapExTools.exe"));
        string other = WriteFile(Path.Combine("Old", "MapExTools.exe"));

        Assert.Equal(ShortcutState.Missing, ToolShortcut.Inspect(desktop, exe).State);

        Assert.False(ToolShortcut.Create(desktop, exe, "desc"));
        ShortcutStatus status = ToolShortcut.Inspect(desktop, exe);
        Assert.Equal(ShortcutState.PointsHere, status.State);
        Assert.Equal(Path.Combine(desktop, ToolShortcut.FileName), status.LinkPath);

        Assert.Equal(ShortcutState.PointsElsewhere, ToolShortcut.Inspect(desktop, other).State);

        // 改为指向另一份：覆盖同一个文件
        ToolShortcut.Create(desktop, other, "desc");
        Assert.Equal(ShortcutState.PointsHere, ToolShortcut.Inspect(desktop, other).State);
        Assert.Single(Directory.GetFiles(desktop));

        ShellLinkInfo? link = ShellLink.TryRead(status.LinkPath);
        Assert.NotNull(link);
        Assert.Equal(Path.GetDirectoryName(other), link.WorkingDirectory, ignoreCase: true);
        Assert.Equal(other, link.IconPath, ignoreCase: true);

        Assert.True(ToolShortcut.Delete(desktop));
        Assert.Equal(ShortcutState.Missing, ToolShortcut.Inspect(desktop, exe).State);
        Assert.False(ToolShortcut.Delete(desktop));
    }

    [Fact]
    public void Shortcut_ExeInLowFolder_IsRaisedToMedium()
    {
        string lowDir = Path.Combine(_root, "LocalLow");
        Directory.CreateDirectory(lowDir);
        FileIntegrity.SetLabel(lowDir, FileIntegrity.LowRid, inheritable: true);
        string exe = WriteFile(Path.Combine("LocalLow", "MapExTools.exe"));

        Assert.True(ToolShortcut.Create(Path.Combine(_root, "Desktop"), exe, "desc"));
        Assert.Equal(FileIntegrity.MediumRid, FileIntegrity.GetLabelRid(exe));
    }

    [Fact]
    public void Shortcut_ForeignLinkWithSameName_IsNotTouched()
    {
        string desktop = Path.Combine(_root, "Desktop");
        Directory.CreateDirectory(desktop);
        string notepad = WriteFile(Path.Combine("Other", "notepad.exe"));
        string linkPath = Path.Combine(desktop, ToolShortcut.FileName);
        ShellLink.Save(linkPath, new ShellLinkInfo(notepad, "", Path.GetDirectoryName(notepad)!, notepad, 0, ""));
        string exe = WriteFile(Path.Combine("Tools", "MapExTools.exe"));

        Assert.Equal(ShortcutState.Foreign, ToolShortcut.Inspect(desktop, exe).State);
        Assert.False(ToolShortcut.Delete(desktop));
        Assert.Throws<InvalidOperationException>(() => ToolShortcut.Create(desktop, exe, "desc"));
        Assert.True(File.Exists(linkPath));
    }

    [Fact]
    public void Shortcut_RenamedCopy_CountsAsOurs()
    {
        string desktop = Path.Combine(_root, "Desktop");
        string renamed = WriteFile(Path.Combine("Tools", "MapExTools (1).exe"));

        ToolShortcut.Create(desktop, renamed, "desc");

        Assert.Equal(ShortcutState.PointsHere, ToolShortcut.Inspect(desktop, renamed).State);
        Assert.True(ToolShortcut.Delete(desktop));
    }
}
