using System.Reflection;
using System.Reflection.Emit;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Conflicts;

public sealed class PlaysetModScannerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly UserDataPaths _paths;

    public PlaysetModScannerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "MapExTools_ScannerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _paths = new UserDataPaths(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            RemoveDirectorySafe(_tempRoot);
        }
    }

    [Fact]
    public void Scan_PicksHighestIntegerVersion_AndResolvesDisplayNameFromCacheAndMetadata()
    {
        string pdxRoot = Path.Combine(_paths.ModsCacheDir, "pdx_mods");
        string localRoot = Path.Combine(_paths.ModsCacheDir, "local");
        Directory.CreateDirectory(pdxRoot);
        Directory.CreateDirectory(localRoot);

        // 构造 86944_9 与 86944_10 两个版本目录：字典序 "9" > "10"，整数序 10 > 9
        string v9Dir = Path.Combine(pdxRoot, "86944_9");
        string v10Dir = Path.Combine(pdxRoot, "86944_10");
        Directory.CreateDirectory(v9Dir);
        Directory.CreateDirectory(v10Dir);

        File.WriteAllBytes(Path.Combine(v9Dir, "SampleMod.dll"), BuildSimpleIMod("SampleMod", new Version(1, 9, 0)));
        File.WriteAllBytes(Path.Combine(v10Dir, "SampleMod.dll"), BuildSimpleIMod("SampleMod", new Version(1, 10, 0)));
        // Burst 原生后缀库应被跳过，不进入 UnreadableAssemblies
        File.WriteAllBytes(Path.Combine(v10Dir, "SampleMod_win_x86_64.dll"), [0x4D, 0x5A, 0x00, 0x00]);

        // SDK local 包带 .metadata/metadata.json
        string sdkLocalDir = Path.Combine(localRoot, "MyLocalPack_1");
        Directory.CreateDirectory(Path.Combine(sdkLocalDir, ".metadata"));
        File.WriteAllBytes(Path.Combine(sdkLocalDir, "MyLocalPack.dll"), BuildSimpleIMod("MyLocalPack", new Version(0, 5, 0)));
        File.WriteAllText(Path.Combine(sdkLocalDir, ".metadata", "metadata.json"),
            """{"displayName":"SDK Local Mod","userModVersion":"0.5.0-local"}""");

        // pdx_mods_cache.json 提供订阅包商店显示名
        File.WriteAllText(_paths.PdxModsCacheFile, """
            {
              "items": [
                { "source": "pdx_mods", "id": "86944", "version": "9", "userModVersion": "1.9.0", "displayName": "Store Sample Old" },
                { "source": "pdx_mods", "id": "86944", "version": "10", "userModVersion": "1.10.0", "displayName": "Store Sample Mod" }
              ]
            }
            """);

        // playset_config.json 包含启用项、禁用项、云端待下载项与非激活 Playset
        File.WriteAllText(_paths.PlaysetConfigFile, """
            {
              "activePlaysetId": "playset-active",
              "playsets": [
                {
                  "id": "playset-other",
                  "presentation": { "name": "Other" },
                  "mods": [ { "source": "pdx_mods", "sourceId": "99999", "isEnabled": true } ]
                },
                {
                  "id": "playset-active",
                  "presentation": { "name": "Main Test Playset" },
                  "mods": [
                    { "source": "pdx_mods", "sourceId": "86944", "isEnabled": true },
                    { "source": "pdx_mods", "sourceId": "77777", "isEnabled": false },
                    { "source": "pdx_mods", "sourceId": "55555", "isEnabled": true },
                    { "source": "local", "sourceId": "MyLocalPack", "isEnabled": true }
                  ]
                }
              ]
            }
            """);

        PlaysetScanResult result = PlaysetModScanner.Scan(_paths, gameDir: null);

        Assert.Equal("Main Test Playset", result.ActivePlaysetName);
        Assert.False(result.ManagedDirectoryAvailable);
        Assert.Empty(result.UnreadableAssemblies);

        EnabledModPackage samplePkg = Assert.Single(result.Packages, p => p.PdxModId == "86944");
        Assert.Equal(10, samplePkg.PlatformVersion);
        Assert.Equal("Store Sample Mod", samplePkg.DisplayName);
        Assert.Equal("1.10.0", samplePkg.DisplayVersion);
        WinningAssemblyEntry sampleWin = Assert.Single(samplePkg.WinningAssemblies);
        Assert.Equal(new Version(1, 10, 0, 0), sampleWin.AssemblyVersion);

        EnabledModPackage pendingPkg = Assert.Single(result.Packages, p => p.PdxModId == "55555");
        Assert.True(pendingPkg.IsPendingCloudDownload);

        EnabledModPackage sdkLocalPkg = Assert.Single(result.Packages, p => p.Source == ModInstallSource.PdxLocal);
        Assert.Equal("SDK Local Mod", sdkLocalPkg.DisplayName);
        Assert.Equal("0.5.0-local", sdkLocalPkg.DisplayVersion);
        Assert.False(Assert.Single(sdkLocalPkg.WinningAssemblies).IsLocal);
    }

    [Fact]
    public void Scan_SkipsDotAndTildeFolders_AndFiltersUnloadableDuplicateBeforeRanking()
    {
        // 准备模拟游戏 Managed/ 目录
        string fakeGameDir = Path.Combine(_tempRoot, "FakeGame");
        string managedDir = Path.Combine(fakeGameDir, "Cities2_Data", "Managed");
        Directory.CreateDirectory(managedDir);

        string runtimeCoreName = typeof(object).Assembly.GetName().Name!;
        File.WriteAllBytes(Path.Combine(managedDir, runtimeCoreName + ".dll"), [0]);
        File.WriteAllBytes(Path.Combine(managedDir, "Unity.Entities.dll"), SyntheticModBuilder.Env.UnityEntitiesBytes);
        File.WriteAllBytes(Path.Combine(managedDir, "Game.dll"), SyntheticModBuilder.Env.GameBytes);
        File.WriteAllBytes(Path.Combine(managedDir, "Newtonsoft.Json.dll"), [0]);

        // 1. Skyve 禁用的 .DisabledMod 与 ~Backup 目录必须跳过
        string skyveDisabled = Path.Combine(_paths.ModsDir, ".DisabledBySkyve");
        string tildeBackup = Path.Combine(_paths.ModsDir, "~BackupMod");
        Directory.CreateDirectory(skyveDisabled);
        Directory.CreateDirectory(tildeBackup);
        File.WriteAllBytes(Path.Combine(skyveDisabled, "ShouldSkip.dll"), BuildSimpleIMod("ShouldSkip", new Version(1, 0, 0)));
        File.WriteAllBytes(Path.Combine(tildeBackup, "ShouldSkip2.dll"), BuildSimpleIMod("ShouldSkip2", new Version(1, 0, 0)));

        // 2. 本地 Mods/DupMod 虽为 isLocal==true 且版本号更高（2.0.0），但引用了不存在的前置 GhostDep，导致 !canBeLoaded
        byte[] ghostDepBytes = SyntheticModBuilder.BuildModAssembly("GhostDep", new Version(1, 0, 0), (m, _) =>
        {
            TypeBuilder tb = m.DefineType("Ghost.Marker", TypeAttributes.Public | TypeAttributes.Class);
            tb.DefineDefaultConstructor(MethodAttributes.Public);
            tb.CreateType();
        });
        Assembly ghostAsm = SyntheticModBuilder.Env.Alc.LoadFromStream(new MemoryStream(ghostDepBytes));
        Type ghostMarker = ghostAsm.GetType("Ghost.Marker")!;

        byte[] brokenLocalDup = SyntheticModBuilder.BuildModAssembly("DupMod", new Version(2, 0, 0), (m, env) =>
        {
            TypeBuilder tb = m.DefineType("Dup.BrokenMod", TypeAttributes.Public | TypeAttributes.Class);
            tb.AddInterfaceImplementation(env.IModType);
            tb.DefineField("_ghost", ghostMarker, FieldAttributes.Private);
            DefineIModMethods(tb, env);
            tb.CreateType();
        });

        string localDupDir = Path.Combine(_paths.ModsDir, "DupMod");
        Directory.CreateDirectory(localDupDir);
        File.WriteAllBytes(Path.Combine(localDupDir, "DupMod.dll"), brokenLocalDup);
        // 与 Managed/ 同名的随包 Newtonsoft.Json.dll（故意写坏字节）：应按 GetModAssets 提前跳过，不报损坏
        File.WriteAllBytes(Path.Combine(localDupDir, "Newtonsoft.Json.dll"), [0x01, 0x02, 0x03]);
        // 另一个畸形非同名 DLL：容错记入 UnreadableAssemblies，不中断扫描
        File.WriteAllBytes(Path.Combine(localDupDir, "CorruptedHelper.dll"), [0xDE, 0xAD, 0xBE, 0xEF]);

        // 3. PDX 订阅版 DupMod（1.0.0）依赖齐全（canBeLoaded == true），应先滤除不可加载的本地版后胜出！
        string pdxRoot = Path.Combine(_paths.ModsCacheDir, "pdx_mods");
        string pdxDupDir = Path.Combine(pdxRoot, "12345_1");
        Directory.CreateDirectory(pdxDupDir);
        File.WriteAllBytes(Path.Combine(pdxDupDir, "DupMod.dll"), BuildSimpleIMod("DupMod", new Version(1, 0, 0)));

        File.WriteAllText(_paths.PlaysetConfigFile, """
            {
              "activePlaysetId": "p1",
              "playsets": [
                {
                  "id": "p1",
                  "presentation": { "name": "DepFilterTest" },
                  "mods": [ { "source": "pdx_mods", "sourceId": "12345", "isEnabled": true } ]
                }
              ]
            }
            """);

        PlaysetScanResult result = PlaysetModScanner.Scan(_paths, fakeGameDir);

        Assert.True(result.ManagedDirectoryAvailable);
        Assert.DoesNotContain(result.WinningAssemblies, a => a.AssemblyName.StartsWith("ShouldSkip", StringComparison.Ordinal));

        // 关键断言：先滤除缺 GhostDep 的本地 2.0.0 副本，选出可加载的订阅 1.0.0 副本！
        WinningAssemblyEntry dupWinner = Assert.Single(result.WinningAssemblies, a => a.AssemblyName == "DupMod");
        Assert.Equal(ModInstallSource.PdxMods, dupWinner.Package.Source);
        Assert.Equal(new Version(1, 0, 0, 0), dupWinner.AssemblyVersion);

        // 损坏的 CorruptedHelper.dll 被记录，而同名的 Newtonsoft.Json.dll 被提前跳过
        UnreadableAssemblyEntry bad = Assert.Single(result.UnreadableAssemblies);
        Assert.EndsWith("CorruptedHelper.dll", bad.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] BuildSimpleIMod(string asmName, Version version) =>
        SyntheticModBuilder.BuildModAssembly(asmName, version, (mod, env) =>
        {
            TypeBuilder tb = mod.DefineType(asmName + ".ModEntry", TypeAttributes.Public | TypeAttributes.Class);
            tb.AddInterfaceImplementation(env.IModType);
            DefineIModMethods(tb, env);
            tb.CreateType();
        });

    private static void DefineIModMethods(TypeBuilder tb, SyntheticModBuilder.StubEnvironment env)
    {
        Type updateSysType = env.GameAsm.GetType("Game.UpdateSystem")!;
        tb.DefineMethod("OnLoad", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), [updateSysType])
          .GetILGenerator().Emit(OpCodes.Ret);
        tb.DefineMethod("OnDispose", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes)
          .GetILGenerator().Emit(OpCodes.Ret);
    }

    private static void RemoveDirectorySafe(string root)
    {
        var dirs = new Stack<string>();
        var postOrder = new Stack<string>();
        dirs.Push(root);
        while (dirs.Count > 0)
        {
            string cur = dirs.Pop();
            postOrder.Push(cur);
            foreach (string f in Directory.GetFiles(cur))
                File.Delete(f);
            foreach (string sub in Directory.GetDirectories(cur))
                dirs.Push(sub);
        }

        while (postOrder.Count > 0)
        {
            Directory.Delete(postOrder.Pop(), recursive: false);
        }
    }
}
