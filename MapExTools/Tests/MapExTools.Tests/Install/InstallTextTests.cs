using MapExTools.Core.Install;
using MapExTools.Core.Localization;

namespace MapExTools.Tests.Install;

/// <summary>运行位置、快捷方式与"固定到本地"确认框的文字（计划 2.3）。</summary>
public sealed class InstallTextTests
{
    private const string Zh = UiLanguage.SimplifiedChinese;

    [Fact]
    public void Location_NormalShowsPathOnly_OthersNameTheKind()
    {
        Assert.Equal(@"位置：D:\Tools\MapExTools.exe",
            InstallText.Location(new InstallLocation(@"D:\Tools\MapExTools.exe", InstallLocationKind.Normal), Zh));
        Assert.Equal(@"位置：D:\Downloads\MapExTools.exe（下载文件夹）",
            InstallText.Location(new InstallLocation(@"D:\Downloads\MapExTools.exe", InstallLocationKind.Downloads), Zh));

        foreach (InstallLocationKind kind in Enum.GetValues<InstallLocationKind>())
        {
            foreach (string language in UiLanguage.All)
                Assert.DoesNotContain("Install.Kind.", InstallText.Location(new InstallLocation(@"C:\x.exe", kind), language));
        }
    }

    [Fact]
    public void Shortcut_EachState()
    {
        Assert.Equal("桌面快捷方式：未创建", InstallText.Shortcut(new ShortcutStatus(ShortcutState.Missing, "a.lnk", null), Zh));
        Assert.Equal("桌面快捷方式：已创建，指向当前位置", InstallText.Shortcut(new ShortcutStatus(ShortcutState.PointsHere, "a.lnk", @"C:\x.exe"), Zh));
        Assert.Equal(@"桌面快捷方式：指向另一个位置（D:\Old\MapExTools.exe）",
            InstallText.Shortcut(new ShortcutStatus(ShortcutState.PointsElsewhere, "a.lnk", @"D:\Old\MapExTools.exe"), Zh));
        Assert.Contains("不会改动它", InstallText.Shortcut(new ShortcutStatus(ShortcutState.Foreign, "a.lnk", @"C:\n.exe"), Zh));
    }

    [Fact]
    public void Version_ThreeParts_OrUnknown()
    {
        Assert.Equal("1.2.3", InstallText.Version(new Version(1, 2, 3, 4), Zh));
        Assert.Equal("1.2", InstallText.Version(new Version(1, 2), Zh));
        Assert.Equal("未知", InstallText.Version(null, Zh));
    }

    [Fact]
    public void PinConfirm_NewCopy()
    {
        var plan = new PinPlan(@"D:\Downloads\MapExTools.exe", @"C:\P\MapExTools\MapExTools.exe", false, false, new Version(1, 0, 0, 0), null);

        Assert.Equal("把 MapExTools.exe 复制到\nC:\\P\\MapExTools\n在桌面创建快捷方式，然后从新位置重新打开吗？",
            InstallText.PinConfirm(plan, hasUnsavedSettings: false, Zh));
    }

    [Fact]
    public void PinConfirm_ReplaceAndUnsaved()
    {
        var plan = new PinPlan(@"D:\Downloads\MapExTools.exe", @"C:\P\MapExTools\MapExTools.exe", true, false,
            new Version(1, 1, 0, 0), new Version(1, 0, 0, 0));

        string text = InstallText.PinConfirm(plan, hasUnsavedSettings: true, Zh);

        Assert.EndsWith("\n\n那里已有的一份（版本 1.0.0）会被当前这份（版本 1.1.0）覆盖。\n\n启动设定里未保存的改动会被放弃。", text);
    }

    [Fact]
    public void PinConfirm_Downgrade()
    {
        var plan = new PinPlan(@"D:\Downloads\MapExTools.exe", @"C:\P\MapExTools\MapExTools.exe", true, false,
            new Version(1, 0, 0, 0), new Version(1, 2, 0, 0));

        Assert.EndsWith("\n\n那里已有的一份较新（版本 1.2.0），当前是版本 1.0.0，覆盖后会退回旧版本。",
            InstallText.PinConfirm(plan, hasUnsavedSettings: false, Zh));
    }

    [Fact]
    public void PinConfirm_SameContent_NoReplaceNote()
    {
        var plan = new PinPlan(@"D:\Downloads\MapExTools.exe", @"C:\P\MapExTools\MapExTools.exe", true, true,
            new Version(1, 0, 0, 0), new Version(1, 0, 0, 0));

        Assert.DoesNotContain("覆盖", InstallText.PinConfirm(plan, hasUnsavedSettings: false, Zh));
    }
}
