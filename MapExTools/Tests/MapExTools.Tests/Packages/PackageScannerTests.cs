using MapExTools.Core.Packages;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Packages;

/// <summary>扫描 Saves／Maps：递归、跳过规则、坏包不中断、guid 重复（计划 4.1、Phase 0.3）。</summary>
public sealed class PackageScannerTests : IDisposable
{
    private const string OtherGuid = "99999999999999999999999999999999";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Scan_RecursesAndSkipsLikeTheGame()
    {
        var paths = new UserDataPaths(_root);
        string saves = Path.Combine(paths.SavesDir, "76561198000000000");
        CokBuilder.Save(["MapExt2"]).Save(Path.Combine(saves, "A.cok"));
        CokBuilder.Save(["MapExt2"], OtherGuid).Save(Path.Combine(saves, "Sub", "B.COK"));
        CokBuilder.Save(["MapExt2"], OtherGuid).Save(Path.Combine(saves, "~Temp.cok"));
        CokBuilder.Save(["MapExt2"], OtherGuid).Save(Path.Combine(saves, ".hidden", "C.cok"));
        CokBuilder.Save(["MapExt2"], OtherGuid).Save(Path.Combine(saves, "~backup", "D.cok"));
        File.WriteAllText(Path.Combine(saves, "notes.txt"), "x");
        File.WriteAllText(Path.Combine(saves, "Broken.cok"), "not a zip");

        PackageScanResult result = PackageScanner.Scan(paths, TestContext.Current.CancellationToken);

        Assert.Equal(["A", "B", "Broken"], result.Packages.Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.All(result.Packages, p => Assert.Equal(PackageFolder.Saves, p.Folder));
        Assert.Equal(PackageProblem.Corrupt, result.Packages.Single(p => p.Name == "Broken").Problem);
    }

    [Fact]
    public void Scan_MissingFolders_ReturnsEmpty()
    {
        Assert.Empty(PackageScanner.Scan(new UserDataPaths(_root), TestContext.Current.CancellationToken).Packages);
    }

    [Fact]
    public void Scan_MapsFolder_KindComesFromContent()
    {
        var paths = new UserDataPaths(_root);
        // 地图目录里放了一个存档：种类按元数据判定，来源目录照记
        CokBuilder.Save(null).Save(Path.Combine(paths.MapsDir, "id", "Misplaced.cok"));

        CokPackageInfo info = Assert.Single(PackageScanner.Scan(paths, TestContext.Current.CancellationToken).Packages);

        Assert.Equal(PackageFolder.Maps, info.Folder);
        Assert.Equal(PackageKind.Save, info.Kind);
    }

    [Fact]
    public void Scan_MarksDuplicateMetadataGuids()
    {
        var paths = new UserDataPaths(_root);
        string dir = Path.Combine(paths.SavesDir, "id");
        string original = CokBuilder.Save(["MapExt2"]).Save(Path.Combine(dir, "WH57km4K_20251112_2.cok"));
        string copy = CokBuilder.Save(["MapExt2"]).Save(Path.Combine(dir, "WH57km4K_20251112_2 (2).cok"));
        CokBuilder.Save(["MapExt2"], OtherGuid).Save(Path.Combine(dir, "Unique.cok"));

        IReadOnlyList<CokPackageInfo> packages = PackageScanner.Scan(paths, TestContext.Current.CancellationToken).Packages;

        Assert.Equal([copy], packages.Single(p => p.Path == original).DuplicatePaths);
        Assert.Equal([original], packages.Single(p => p.Path == copy).DuplicatePaths);
        Assert.Empty(packages.Single(p => p.Name == "Unique").DuplicatePaths);
    }

    [Fact]
    public void Scan_Cancelled_Throws()
    {
        var paths = new UserDataPaths(_root);
        CokBuilder.Save(null).Save(Path.Combine(paths.SavesDir, "id", "A.cok"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => PackageScanner.Scan(paths, cts.Token));
    }
}
